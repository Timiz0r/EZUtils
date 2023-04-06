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

        private GetTextDocument(IReadOnlyList<GetTextEntry> entries)
        {
            if (entries.Count > 0 && entries[0].Id != string.Empty) throw new InvalidOperationException("The first entry must be a header.");
            Entries = entries;
            Header = GetTextHeader.FromEntry(entries[0]);
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
            List<GetTextEntry> entries = new List<GetTextEntry>();
            List<GetTextLine> currentEntryLines = new List<GetTextLine>();
            List<GetTextLine> currentCommentBlock = new List<GetTextLine>();
            foreach (GetTextLine line in lines)
            {
                if (line.IsCommentOrWhiteSpace)
                {
                    currentCommentBlock.Add(line);
                    continue;
                }
                //so is a string or a keyworded line after this point

                string keyword = line.Keyword?.Keyword;

                if (!haveHeader && keyword == "msgctxt") throw new InvalidOperationException(
                    "The first entry should be the header, and the header should not have a msgctxt.");
                if (!haveHeader && keyword == "msgid")
                {
                    if (line.StringValue.Raw != string.Empty) throw new InvalidOperationException(
                        $"First entry must be header. Found: {line.RawLine}");
                    haveHeader = true;

                    MoveCurrentCommentBlockToCurrentEntry();
                    processingContextualEntry = false;
                    currentEntryLines.Add(line);

                    continue;
                }
                //ofc cannot be the header after this point

                if (keyword == "msgctxt")
                {
                    StartProcessingNextEntry();
                    processingContextualEntry = true;
                    currentEntryLines.Add(line);
                }
                else if (keyword == "msgid")
                {
                    if (!processingContextualEntry)
                    {
                        StartProcessingNextEntry();
                        processingContextualEntry = false;
                        currentEntryLines.Add(line);
                    }

                    //could use a better name if we can think of one
                    //this local basically indicates that we hit a msgctxt and are also expecting a msgid
                    //once we hit the current entry's msgid, then the next msgid would be for a new entry
                    processingContextualEntry = false;
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

            void StartProcessingNextEntry()
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
            }
            void FinishCurrentEntry()
            {
                GetTextEntry entry = GetTextEntry.Parse(currentEntryLines.ToArray());
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
