namespace EZUtils.Localization
{
    using System;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    //TODO: it kinda makes more sense to integrate this into the doc, instead of having a separate class
    //this design decision mainly depends on how we go about generating documenting for new languages
    public class GetTextHeader
    {
        public CultureInfo Locale { get; }

        public GetTextHeader(CultureInfo locale)
        {
            Locale = locale;
        }

        //we dont currently allow custom plural forms or charsets
        //and otherwise have no other potentially useful data atm

        public GetTextEntry ToEntry(int pluralForms)
        {
            if (pluralForms < 1) throw new ArgumentOutOfRangeException(nameof(pluralForms), "There must be at least one plural form.");

            StringBuilder sb = new StringBuilder();
            sb.Append($"Language: {Locale.TwoLetterISOLanguageName}\n");
            sb.Append("MIME-Version: 1.0\n");
            sb.Append("Content-Type: text/plain; charset=UTF-8\n");
            sb.Append("Content-Transfer-Encoding: 8bit\n");
            sb.Append($"Plural-Forms: nplurals={pluralForms}; ");
            for (int i = 1; i < pluralForms; i++)
            {
                sb.Append($"n == {i} ? {i} : ");
            }
            sb.Append("0;\n");

            GetTextEntry entry = new GetTextEntryBuilder()
                .ConfigureId(string.Empty)
                .ConfigureValue(sb.ToString())
                .Create();
            return entry;
        }

        public static GetTextHeader FromEntry(GetTextEntry entry)
        {
            CultureInfo locale = null;
            Match match = Regex.Match(entry.Value, "(?i)Language: (?'lang'[a-z]+)(?:_(?'country'[a-z]+))?(?:@(?'variant'[a-z]+))?");
            if (match.Success)
            {
                string code = match.Groups["lang"].Value;
                if (match.Groups["variant"].Success)
                {
                    //did this by eye scanning over all cultures; not so authoritative
                    string variant = match.Groups["variant"].Value;
                    if (variant == "latin") variant = "Latn";
                    if (variant == "cyrillic") variant = "Cyrl";
                    if (variant == "adlam") variant = "Adlm";
                    if (variant == "javanese") variant = "Java";
                    if (variant == "arabic") variant = "Arab";
                    if (variant == "devanagari") variant = "Deva";
                    if (variant == "mongolian") variant = "Mong";
                    if (variant == "bangla") variant = "Beng";
                    if (variant == "gurmukhi") variant = "Guru";
                    if (variant == "olchiki") variant = "Olck";
                    if (variant == "tifinagh") variant = "Tfng";
                    if (variant == "vai") variant = "Vaii";
                    if (variant == "simplified") variant = "Hans";
                    if (variant == "traditional") variant = "Hant";

                    code = $"{code}-{variant}";
                }
                if (match.Groups["country"].Success)
                {
                    code = $"{code}-{match.Groups["country"].Value}";
                }
                locale = CultureInfo.GetCultureInfo(code);
            }

            GetTextHeader result = new GetTextHeader(locale);
            return result;
        }
    }
}
