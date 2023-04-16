namespace EZUtils.Localization
{
    using System.Collections.Generic;
    using System.Linq;

    public class GetTextEntryHeader
    {
        public IReadOnlyList<string> References { get; }

        public GetTextEntryHeader(IReadOnlyList<string> references)
        {
            References = references;
        }

        //could hypothetically accept header-only lines, but this is certainly more flexible
        public static GetTextEntryHeader ParseEntryLines(IReadOnlyList<GetTextLine> entryLines)
        {
            //could hypothetically also accept generated comments anywhere in the entry, but meh
            string[] headerComments = entryLines
                .TakeWhile(l => l.IsCommentOrWhiteSpace && !l.IsMarkedObsolete)
                .Where(l => l.IsComment)
                .Select(l => l.Comment)
                .ToArray();

            string[] references = headerComments
                .Where(c => c.StartsWith(":"))
                .Select(c => c.Substring(1).Trim())
                .ToArray();

            GetTextEntryHeader header = new GetTextEntryHeader(references);
            return header;
        }
    }
}
