namespace EZUtils.Localization
{
    using System;
    using System.Text.RegularExpressions;

    //TODO: dont know how mutating will be done yet
    //am thinking either (or both)
    //* deletion and insertion
    //* adding mutation instructions that get comitted in the end
    //kind of mutations?
    //  for headers, it's more like we'll initialize them once. except maybe revision date. but really we got git. so let's not.
    //  inserting new entries (to document)
    //  commenting out removed entries (replacing entry in document)
    //  updating reference comments (could also replace)
    //so yeh simple seems better; won't really be modifying existing entries much if at all
    public class GetTextString
    {
        public string Raw { get; }
        public string Value { get; }

        private GetTextString(string raw, string value)
        {
            Raw = raw;
            Value = value;
        }

        public static GetTextString FromRaw(string raw)
        {
            string value = Regex.Replace(
                raw,
                @"
                (?x)\\(
                a
                | b
                | e
                | f
                | n
                | r
                | t
                | v
                | \\
                | '
                | ""
                | \?
                | (?'oct' [0-7]{1,3})
                | x(?'hex' [0-9a-f]{2})
                | . #invalid escape sequence
                )
                ",
                m =>
                {
                    string basicEscape =
                        m.Value == @"\a" ? "\a" :
                        m.Value == @"\b" ? "\b" :
                        m.Value == @"\e" ? "\x1b" :
                        m.Value == @"\f" ? "\f" :
                        m.Value == @"\n" ? "\n" :
                        m.Value == @"\r" ? "\r" :
                        m.Value == @"\t" ? "\t" :
                        m.Value == @"\v" ? "\v" :
                        m.Value == @"\\" ? "\\" :
                        m.Value == @"\'" ? "'" :
                        m.Value == @"\""" ? "\"" :
                        m.Value == @"\?" ? "?" :
                        (
                            m.Groups["oct"] is Group octGroup && octGroup.Success
                                ? Convert.ToChar(Convert.ToByte(octGroup.Value, 8)).ToString()
                                : m.Groups["hex"] is Group hexGroup && hexGroup.Success
                                    ? Convert.ToChar(Convert.ToByte(hexGroup.Value, 16)).ToString()
                                    : throw new InvalidOperationException($"Invalid escape sequence '{m.Value}'")
                        );
                    return basicEscape;
                });
            return new GetTextString(raw, value);
        }
        public static GetTextString FromValue(string value) => throw new NotImplementedException();
    }
}
