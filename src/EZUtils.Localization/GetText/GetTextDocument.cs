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
            Header = GetTextHeader.FromEntry(entries[0]);
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

            //TODO: keep in mind the case of obsolete entries which are commented out
            //it would be handy to support for two reasons:
            //  deprecating entries ourselves. without it, we'd have to convert an entry to all comments and slap it on another entry
            //  keeping entries sorted by file and line-ish (not fleshed out at time of writing)
            //but it's hard to implement because it complicates new entry detection
            //in reality, perhaps the best way to implement it is in the step where we split comment blocks between entries

            //serves the dual purpose of making sure we start with the header and some special handling around first entry
            bool haveHeader = false;
            bool processingContextualEntry = false;
            bool processingObsoleteEntry = false;
            HashSet<(string context, string id)> parsedIds = new HashSet<(string context, string id)>();
            List<GetTextEntry> entries = new List<GetTextEntry>();
            List<GetTextLine> currentEntryLines = new List<GetTextLine>();
            List<GetTextLine> currentCommentBlock = new List<GetTextLine>();
            foreach (GetTextLine line in lines)
            {
                if (line.IsMarkedObsolete)
                {
                    if (!processingObsoleteEntry)
                    {
                        //since entries as a whole are marked obsolete at a time, not parts
                        //we dont expect to see, for instance, msgstr obsolete without its msgid also being obsolete.
                        //as such, the first time we hit an obsolete comment indicates we've started a new entry.
                        StartProcessingNextEntry(nextEntryContextual: false, nextEntryObsolete: true);
                    }

                    MoveCurrentCommentBlockToCurrentEntry();
                    processingObsoleteEntry = true;
                    currentEntryLines.Add(line);
                    continue;
                }

                if (line.IsCommentOrWhiteSpace)
                {
                    currentCommentBlock.Add(line);
                    continue;
                }
                //is a string or a keyworded line after this point

                string keyword = line.Keyword?.Keyword;

                if (!haveHeader && keyword == "msgctxt") throw new InvalidOperationException(
                    "The first entry should be the header, and the header should not have a msgctxt.");
                if (!haveHeader && keyword == "msgid")
                {
                    if (line.StringValue.Raw != string.Empty) throw new InvalidOperationException(
                        $"First entry must be header. Found: {line.RawLine}");
                    haveHeader = true;

                    MoveCurrentCommentBlockToCurrentEntry();
                    currentEntryLines.Add(line);
                    continue;
                }
                //cannot be the header after this point; is string or keyworded line

                if (keyword == "msgctxt")
                {
                    if (processingContextualEntry) throw new InvalidOperationException(
                        "Found two consecutive 'msgctxt' keyworded entries in a row without a 'msgid' between them.");

                    StartProcessingNextEntry(nextEntryContextual: true, nextEntryObsolete: false);
                    currentEntryLines.Add(line);
                }
                else if (keyword == "msgid")
                {
                    //could use a better name if we can think of one
                    //this local basically indicates that we hit a msgctxt and are also expecting a msgid
                    //once we hit the current entry's msgid, then the next msgid would be for a new entry
                    if (!processingContextualEntry)
                    {
                        StartProcessingNextEntry(nextEntryContextual: false, nextEntryObsolete: false);
                    }

                    //the important implication being that if we were contextual, we no longer are
                    processingContextualEntry = false;
                    currentEntryLines.Add(line);
                }
                else
                {
                    //at this point, string lines and other keyworded lines, like strs and plural stuff
                    MoveCurrentCommentBlockToCurrentEntry();
                    currentEntryLines.Add(line);
                }
            }

            MoveCurrentCommentBlockToCurrentEntry();
            FinishCurrentEntry();

            GetTextDocument document = new GetTextDocument(entries);
            return document;

            void StartProcessingNextEntry(bool nextEntryContextual, bool nextEntryObsolete)
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
                processingObsoleteEntry = nextEntryObsolete;
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
    }
}