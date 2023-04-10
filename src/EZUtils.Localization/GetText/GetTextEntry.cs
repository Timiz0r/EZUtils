namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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
        public string Context { get; }
        public string Id { get; }
        public string PluralId { get; }
        //null if plural
        public string Value { get; }
        public IReadOnlyList<string> PluralValues { get; }
        public IReadOnlyList<GetTextLine> Lines { get; }

        //is only public because of the builder. not many good alternatives.
        //but since users should prefer the builder over this, not really a problem.
        public GetTextEntry(
            IReadOnlyList<GetTextLine> lines,
            GetTextEntryHeader header,
            string context,
            string id,
            string pluralId,
            string value,
            IReadOnlyList<string> pluralValues)
        {
            Lines = lines;
            Header = header;
            Context = context;
            Id = id;
            PluralId = pluralId;
            Value = value;
            PluralValues = pluralValues;
        }

        public string GetFormattedValue(CultureInfo locale, params object[] args) => string.Format(locale, Value, args);

        public static GetTextEntry Parse(IReadOnlyList<GetTextLine> lines)
        {
            Dictionary<string, StringBuilder> keywordMap = new Dictionary<string, StringBuilder>();
            Dictionary<int, StringBuilder> pluralMap = new Dictionary<int, StringBuilder>();
            StringBuilder currentKeyword = null;
            foreach (GetTextLine line in lines)
            {
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

            int expectedPluralCount = pluralMap.Count == 0 ? 0 : pluralMap.Keys.Max() + 1;
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
