namespace EZUtils.Localization
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    public class GetTextLine
    {
        public GetTextKeyword Keyword { get; }
        public GetTextString StringValue { get; }
        //note that while we support inline comments, poedit doesn't. as such, wouldn't recommend writing them in practice.
        //and we won't be generating them ourselves.
        public string Comment { get; }
        public string RawLine { get; }

        public bool IsCommentOrWhiteSpace { get; }
        public bool IsWhiteSpace { get; }
        public bool IsComment { get; }
        public bool IsMarkedObsolete { get; }

        private GetTextLine(GetTextKeyword keyword, GetTextString stringValue, string comment, string rawLine)
        {
            Keyword = keyword;
            StringValue = stringValue;
            Comment = comment;
            RawLine = rawLine;

            IsCommentOrWhiteSpace = keyword == null && stringValue == null;
            IsWhiteSpace = IsCommentOrWhiteSpace && comment == null;
            IsComment = IsCommentOrWhiteSpace && comment != null;
            IsMarkedObsolete = IsComment && Comment.StartsWith("~");
        }
        public GetTextLine(GetTextKeyword keyword, GetTextString stringValue, string comment)
            : this(keyword, stringValue, comment, string.Concat(
                keyword == null
                    ? string.Empty
                    : keyword.Index is int i
                        ? $"{keyword}[{i}]"
                        : keyword.Keyword,
                stringValue == null
                    ? string.Empty
                    : '"' + stringValue.Raw + '"',
                comment == null
                    ? string.Empty
                    : '#' + comment
            ))
        {}
        public GetTextLine(string comment) : this(keyword: null, stringValue: null, comment)
        {}
        public GetTextLine(GetTextString stringValue) : this(keyword: null, stringValue: stringValue, comment: null)
        {}
        public GetTextLine(GetTextKeyword keyword, GetTextString stringValue) : this(keyword: keyword, stringValue: stringValue, comment: null)
        {}

        public static GetTextLine Parse(string line)
            => TryParse(line, out GetTextLine result) ? result : throw new InvalidOperationException($"Invalid line: {line}");

        public static bool TryParse(string line, out GetTextLine result)
        {
            Match match = Regex.Match(
                line,
                @"(?x)^
                (?: #keyword (msgid, etc.) and optional index
                    (?>\s*)(?'keyword'[^\s\[""\#]+) #could try [a-zA-Z0-9] or something, as well
                    (?>\s*)(?>\[(?'index'\d+)\])?
                )?
                (?>\s*)(?:""
                    (?'string'(?>[^""]|(?<=\\)"")*)
                "")?
                (?>\s*)(?:
                    \#(?'comment'.*)
                )?
                ");
            if (!match.Success)
            {
                result = null;
                return false;
            }

            result = new GetTextLine(
                match.Groups["keyword"].Success
                    ? new GetTextKeyword(
                        match.Groups["keyword"].Value,
                        match.Groups["index"].Success
                            ? int.Parse(match.Groups["index"].Value, NumberStyles.None, CultureInfo.InvariantCulture)
                            : (int?)null)
                    : null,
                stringValue: match.Groups["string"].Success
                    ? GetTextString.FromRaw(match.Groups["string"].Value)
                    : null,
                comment: match.Groups["comment"].Success
                    ? match.Groups["comment"].Value
                    : null,
                rawLine: line);
            return true;
        }
    }
}