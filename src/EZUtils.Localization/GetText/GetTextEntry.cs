namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class GetTextEntry
    {
        private static readonly HashSet<string> supportedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "msgctxt",
            "msgid",
            "msgid_plural",
            "msgstr",
        };

        public GetTextEntryHeader Header { get; }
        public bool IsObsolete { get; }
        public string Context { get; }
        public string Id { get; }
        public string PluralId { get; }
        //null if plural
        public string Value { get; }
        public IReadOnlyList<string> PluralValues { get; }
        public IReadOnlyList<GetTextLine> Lines { get; }

        //is not private because builder needs it
        //is not public because we prefer users provide lines to parse, in order to maintain strict control
        //over how lines and entries are mapped to each other.
        //also isn't yet a use-case for making it public; not a dealbreaker though.
        internal GetTextEntry(
            IReadOnlyList<GetTextLine> lines,
            GetTextEntryHeader header,
            bool isObsolete,
            string context,
            string id,
            string pluralId,
            string value,
            IReadOnlyList<string> pluralValues)
        {
            Lines = lines;
            Header = header;
            IsObsolete = isObsolete;
            Context = context;
            Id = id;
            PluralId = pluralId;
            Value = value;
            //plural id is what determines if an entry is for a plural or not
            //for small reasons not worth mentioning, it's convenient for this not to be null
            PluralValues = pluralValues ?? Array.Empty<string>();
        }

        public static GetTextEntry Parse(IReadOnlyList<GetTextLine> lines)
        {
            Dictionary<string, StringBuilder> keywordMap = new Dictionary<string, StringBuilder>();
            Dictionary<int, StringBuilder> pluralMap = new Dictionary<int, StringBuilder>();
            StringBuilder currentKeyword = null;
            bool hasObsoleteLines = false;
            foreach (GetTextLine currentLine in lines ?? Enumerable.Empty<GetTextLine>())
            {
                GetTextLine line = currentLine;
                if (currentLine.IsMarkedObsolete)
                {
                    line = GetTextLine.Parse(currentLine.Comment.Substring(2));
                    hasObsoleteLines = true;
                }

                if (line.Keyword?.Keyword is string keyword)
                {
                    if (line.Keyword?.Index == null)
                    {
                        if (keywordMap.ContainsKey(keyword)) throw new InvalidOperationException(
                            $"Keyword '{keyword}' already appeared in the entry prior to line: {line.RawLine}");
                        currentKeyword = keywordMap[keyword] = new StringBuilder();
                    }
                    else if (line.Keyword?.Index is int index)
                    {
                        if (keyword != "msgstr") throw new NotImplementedException($"Indexed lines are only supported for keyword 'msgstr'. Line: {line.RawLine}");
                        if (pluralMap.ContainsKey(index)) throw new InvalidOperationException(
                            $"Plural '{index}' already appeared in the entry prior to line: {line.RawLine}.");
                        currentKeyword = pluralMap[index] = new StringBuilder();
                    }
                }

                if (line.StringValue?.Value is string value)
                {
                    if (currentKeyword == null) throw new InvalidOperationException(
                        $"Found a string-only line without a prior keyworded line: {line.RawLine}");
                    _ = currentKeyword.Append(value);
                }
            }

            //we check this after the loop in case the entry starts with non-obsolete lines but has obsolete ones later
            if (hasObsoleteLines && lines.Any(l => !l.IsCommentOrWhiteSpace)) throw new InvalidOperationException(
                "Entry has lines marked obsolete but has non-obsolete lines as well.");

            int expectedPluralCount = pluralMap.Count == 0 ? 0 : pluralMap.Keys.Max() + 1; //so if highest key is 2, expected count 3 (0, 1, 2)
            if (pluralMap.Count != expectedPluralCount) throw new InvalidOperationException(
                $"Expected a plural count of '{expectedPluralCount}' based on the highest found index, but only found '{pluralMap.Count}' plural entries.");
            if (keywordMap.ContainsKey("msgid_plural") && pluralMap.Count == 0) throw new InvalidOperationException("Plural id provided, but no plurals found.");
            if (pluralMap.Count > 0 && !keywordMap.ContainsKey("msgid_plural")) throw new InvalidOperationException("Plurals provided, but no plural id found.");

            //dont have to throw on unknown keywords, but it indicates possible user error that should be flagged, like a typo
            string[] unknownKeywords = keywordMap.Keys
                .Where(k => !supportedKeywords.Contains(k))
                .ToArray();
            if (unknownKeywords.Length > 0) throw new InvalidOperationException($"Found unsupported keywords in entry: {string.Join(", ", unknownKeywords)}");

            GetTextEntry entry = new GetTextEntry(
                lines: lines,
                header: GetTextEntryHeader.ParseEntryLines(lines),
                isObsolete: hasObsoleteLines,
                context: GetValue("msgctxt"),
                id: GetValue("msgid"),
                pluralId: GetValue("msgid_plural"),
                value: GetValue("msgstr"),
                pluralValues: pluralMap
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => kvp.Value.ToString())
                    .ToArray()
            );
            return entry;

            string GetValue(string keyword)
                => keywordMap.TryGetValue(keyword, out StringBuilder value)
                    ? value.ToString()
                    : null;
        }
    }
}
