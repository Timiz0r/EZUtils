namespace EZUtils.Localization
{
    using System.Collections.Generic;
    using System.Linq;

    public class GetTextEntryHeader
    {
        public IReadOnlyList<string> References { get; }
        public IReadOnlyList<string> Flags { get; }

        public GetTextEntryHeader(IReadOnlyList<string> references, IReadOnlyList<string> flags)
        {
            References = references;
            Flags = flags;
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

            string unsplitFlags = headerComments.SingleOrDefault(c => c.StartsWith(",")) ?? string.Empty;
            //not 100% sure if split and trim is what we should do
            //but the documented flags dont have spaces, so might as well
            string[] flags = unsplitFlags
                .Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .ToArray();

            GetTextEntryHeader header = new GetTextEntryHeader(references, flags);
            return header;
        }
    }
}
