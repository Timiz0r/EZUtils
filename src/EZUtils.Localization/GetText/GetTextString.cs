namespace EZUtils.Localization
{
    using System;
    using System.Text.RegularExpressions;

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
                @"(?x)\\(
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
        public static GetTextString FromValue(string value)
        {
            string raw = Regex.Replace(
                value,
                @"(?x)(
                \a
                | \x08 #backspace \b
                | \x1b #\e
                | \f
                | \n
                | \r
                | \t
                | \v
                | \\
                #| '  doesnt really matter for us
                | ""
                #| \?  probably doesnt matter for us
                | [\x00-\x1f^\a\x08\f\n\r\t\v]
                | \x7f #DEL char
                )
                ",
                m =>
                {
                    string characterOfInterest =
                        m.Value == "\a" ? @"\a" :
                        m.Value == "\b" ? @"\b" :
                        m.Value == "\x1b" ? @"\e" :
                        m.Value == "\f" ? @"\f" :
                        m.Value == "\n" ? @"\n" :
                        m.Value == "\r" ? @"\r" :
                        m.Value == "\t" ? @"\t" :
                        m.Value == "\v" ? @"\v" :
                        m.Value == @"\" ? @"\\" :
                        m.Value == @"""" ? @"\""" :
                        char.IsControl(m.Value[0]) && Convert.ToByte(m.Value[0]).ToString("x2") is string hexValue
                            ? $@"\x{hexValue}"
                            : throw new InvalidOperationException($"Cannot handle character '{Convert.ToByte(m.Value[0]):x2}'.");

                    return characterOfInterest;
                });
            return new GetTextString(raw, value);
        }
    }
}
