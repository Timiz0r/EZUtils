namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class GetTextDocument
    {
        //note that we don't maintain superfluous whitespace, because there's no compelling reason to try to
        //  though we could if each element had a tryparse that could then store the unparsed value to be returned in IElement.Value
        //  but then, in order to keep things immutable, the implementations of elements containing GetTextStrings get somewhat obnoxious
        //  as we accumulate strings split across multiple lines
        //since the file is line-delimited, we try to handle it as such
        //we are theoretically more permissive than what's intended with regards to...
        //  extra whitespace (not that we maintain it)
        //  comments between strings

        //note that the header wraps the entry that exists in `entries`
        public GetTextHeader Header { get; }
        public IReadOnlyList<GetTextEntry> Entries { get; }

        public GetTextDocument(IReadOnlyList<GetTextEntry> entries)
        {
            if (entries.Count > 0 && entries[0].Id != string.Empty) throw new InvalidOperationException("The first entry must be a header.");
            Entries = entries;
            Header = new GetTextHeader(entries[0]);
        }

        //in our implementation, we dont consider the plural id an actual id
        //mainly because we dont have an explicit plural id because we accept multiple plural forms
        public GetTextEntry FindEntry(string id) => FindEntry(context: null, id: id);
        public GetTextEntry FindEntry(string context, string id)
            => FindEntry(context: context, id: id, out GetTextEntry entry) >= 0
                ? entry
                : null;
        public int FindEntry(string id, out GetTextEntry entry)
            => FindEntry(context: null, id: id, out entry);
        public int FindEntry(string context, string id, out GetTextEntry entry)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Context == context && Entries[i].Id == id)
                {
                    entry = Entries[i];
                    return i;
                }
            }

            entry = null;
            return -1;
        }

        public static GetTextDocument Parse(string document)
        {
            using (StringReader sr = new StringReader(document))
            {
                return LoadFrom(sr);
            }
        }

        public static GetTextDocument LoadFrom(string path)
        {
            using (StreamReader sr = new StreamReader(path))
            {
                return LoadFrom(sr);
            }
        }

        public static GetTextDocument LoadFrom(TextReader reader)
        {
            List<GetTextLine> lines = new List<GetTextLine>();

            string rawLine;
            while ((rawLine = reader.ReadLine()) != null)
            {
                lines.Add(GetTextLine.Parse(rawLine));
            }

            //serves the dual purpose of making sure we start with the header and some special handling around first entry
            bool haveHeader = false;
            bool processingContextualEntry = false;
            HashSet<(string context, string id)> parsedIds = new HashSet<(string context, string id)>();
            List<GetTextEntry> entries = new List<GetTextEntry>();
            List<GetTextLine> currentEntryLines = new List<GetTextLine>();
            List<GetTextLine> currentCommentBlock = new List<GetTextLine>();
            foreach (GetTextLine actualLine in lines)
            {
                //NOTE: our implementation around obsolete entries is somewhat inefficient
                //is requires an extra couple parses for a deprecated line: one here and one in GetTextEntry.Parse
                //we could change how we parse GetTextLine to have special handling if line raw line starts with #~,
                //but we picked the most clean design under the assumption that perf will not be an issue.
                //
                //lineToInspect should be used when reading a line's properties.
                //actualLine, being the actual line, is what should be passed over to entries
                GetTextLine lineToInspect = actualLine.IsMarkedObsolete
                    ? GetTextLine.Parse(actualLine.Comment.Substring(1))
                    : actualLine;

                if (lineToInspect.IsCommentOrWhiteSpace)
                {
                    currentCommentBlock.Add(actualLine);
                    continue;
                }
                //is a string or a keyworded line after this point

                //since the header cannot be deprecated, we inspect the actual line here
                if (!haveHeader && actualLine.Keyword?.Keyword == "msgctxt") throw new InvalidOperationException(
                    "The first entry should be the header, and the header should not have a msgctxt.");
                if (!haveHeader && actualLine.Keyword?.Keyword == "msgid")
                {
                    if (actualLine.StringValue.Raw != string.Empty) throw new InvalidOperationException(
                        $"First entry must be header. Found: {actualLine.RawLine}");
                    haveHeader = true;

                    MoveCurrentCommentBlockToCurrentEntry();
                    AddCurrentLineToCurrentEntry();
                    continue;
                }
                //cannot be the header after this point; is string or keyworded line

                string keyword = lineToInspect.Keyword?.Keyword;
                if (keyword == "msgctxt")
                {
                    if (processingContextualEntry) throw new InvalidOperationException(
                        "Found two consecutive 'msgctxt' keyworded entries in a row without a 'msgid' between them.");

                    StartProcessingNextEntry(nextEntryContextual: true);
                    AddCurrentLineToCurrentEntry();
                }
                else if (keyword == "msgid")
                {
                    //could use a better name if we can think of one
                    //this local basically indicates that we hit a msgctxt and are also expecting a msgid
                    //once we hit the current entry's msgid, then the next msgid would be for a new entry
                    if (!processingContextualEntry)
                    {
                        StartProcessingNextEntry(nextEntryContextual: false);
                    }

                    //the important implication being that if we were contextual, we no longer are
                    processingContextualEntry = false;
                    AddCurrentLineToCurrentEntry();
                }
                else
                {
                    //at this point, string lines and other keyworded lines, like strs and plural stuff
                    MoveCurrentCommentBlockToCurrentEntry();
                    AddCurrentLineToCurrentEntry();
                }
                void AddCurrentLineToCurrentEntry() => currentEntryLines.Add(actualLine);
            }

            MoveCurrentCommentBlockToCurrentEntry();
            FinishCurrentEntry();

            GetTextDocument document = new GetTextDocument(entries);
            return document;

            void StartProcessingNextEntry(bool nextEntryContextual)
            {
                bool foundWhiteSpace = false;
                foreach (GetTextLine comment in currentCommentBlock)
                {
                    if (!foundWhiteSpace && comment.IsWhiteSpace)
                    {
                        foundWhiteSpace = true;
                        FinishCurrentEntry();
                    }

                    //in the event we found whitespace, that piece of whitespace starts the now current entry
                    currentEntryLines.Add(comment);
                }
                currentCommentBlock.Clear();

                if (!foundWhiteSpace)
                {
                    FinishCurrentEntry();
                }

                processingContextualEntry = nextEntryContextual;
            }
            void FinishCurrentEntry()
            {
                GetTextEntry entry = GetTextEntry.Parse(currentEntryLines.ToArray());

                if (!parsedIds.Add((context: entry.Context, id: entry.Id))) throw new InvalidOperationException(
                    $"Duplicate entry found. Context: '{entry.Context}', Id: '{entry.Id}'");

                currentEntryLines.Clear();
                entries.Add(entry);
            }
            void MoveCurrentCommentBlockToCurrentEntry()
            {
                currentEntryLines.AddRange(currentCommentBlock);
                currentCommentBlock.Clear();
            }
        }

        public void Save(string path)
        {
            using (StreamWriter sw = new StreamWriter(path))
            {
                foreach (GetTextEntry entry in Entries)
                {
                    foreach (GetTextLine line in entry.Lines)
                    {
                        sw.WriteLine(line.RawLine);
                    }
                }
            }
        }
    }
}
