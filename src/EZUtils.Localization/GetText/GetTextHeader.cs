namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    //TODO: it kinda makes more sense to integrate this into the doc, instead of having a separate class
    //this design decision mainly depends on how we go about generating documenting for new languages
    public class GetTextHeader
    {
        //did this by eye scanning over all cultures; not so authoritative
        private static readonly List<(string getTextVariant, string cultureInfoVariant)> variants = new List<(string getTextVariant, string cultureInfoVariant)>()
        {
            ("latin", "Latn"),
            ("cyrillic", "Cyrl"),
            ("adlam", "Adlm"),
            ("javanese", "Java"),
            ("arabic", "Arab"),
            ("devanagari", "Deva"),
            ("mongolian", "Mong"),
            ("bangla", "Beng"),
            ("gurmukhi", "Guru"),
            ("olchiki", "Olck"),
            ("tifinagh", "Tfng"),
            ("vai", "Vaii"),
            ("simplified", "Hans"),
            ("traditional", "Hant"),
        };
        public Locale Locale { get; }

        public GetTextHeader(Locale locale)
        {
            Locale = locale;
        }

        //we dont currently allow custom plural forms or charsets
        //and otherwise have no other potentially useful data atm

        public GetTextEntry ToEntry()
        {

            StringBuilder sb = new StringBuilder();
            string language = GetGetTextLanguageFromCultureInfo(Locale.CultureInfo);
            _ = sb.Append($"Language: {language}\n");
            _ = sb.Append("MIME-Version: 1.0\n");
            _ = sb.Append("Content-Type: text/plain; charset=UTF-8\n");
            _ = sb.Append("Content-Transfer-Encoding: 8bit\n");

            //we generate these so that poedit or other tools can see the plural strings property
            int pluralFormCount = Locale.PluralRules.Count;
            if (Locale.UseSpecialZero) pluralFormCount++;
            if (pluralFormCount < 1) throw new InvalidOperationException("There must be at least one plural form.");
            _ = sb.Append($"Plural-Forms: nplurals={pluralFormCount}; ");
            for (int i = 1; i < pluralFormCount; i++)
            {
                _ = sb.Append($"n == {i} ? {i} : ");
            }
            _ = sb.Append("0;\n");

            GetTextEntry entry = new GetTextEntryBuilder()
                .ConfigureId(string.Empty)
                .ConfigureValue(sb.ToString())
                .Create();
            return entry;
        }

        public static GetTextHeader FromEntry(GetTextEntry entry)
        {
            Match match = Regex.Match(entry.Value, "(?i)Language: (?'lang'[a-z]+)(?:_(?'country'[a-z]+))?(?:@(?'variant'[a-z]+))?");
            CultureInfo cultureInfo = match.Success
                ? GetCultureInfoFromGetTextLanguage(
                    language: match.Groups["lang"].Value,
                    country: match.Groups["country"].Value,
                    variant: match.Groups["variant"].Value)
                //is basically the most important field
                : throw new InvalidOperationException($"Language field not found.");

            //TODO: read plural rules
            Locale locale = new Locale(cultureInfo);
            GetTextHeader result = new GetTextHeader(locale);
            return result;
        }

        private static CultureInfo GetCultureInfoFromGetTextLanguage(string language, string country, string variant)
        {
            string code = language;
            if (!string.IsNullOrEmpty(variant))
            {
                variant = variants
                    .SingleOrDefault(v => v.getTextVariant.Equals(variant, StringComparison.OrdinalIgnoreCase))
                    .cultureInfoVariant
                    ?? throw new InvalidOperationException($"Unsupported variant '{variant}'.");
                code = $"{code}-{variant}";
            }
            if (!string.IsNullOrEmpty(country))
            {
                code = $"{code}-{country}";
            }

            CultureInfo result = CultureInfo.GetCultureInfo(code);
            return result;
        }

        private static string GetGetTextLanguageFromCultureInfo(CultureInfo cultureInfo)
        {
            string[] components = cultureInfo.Name.Split('-');
            string language = components[0];
            string country = null;
            string variant = null;
            if (components.Length == 2)
            {
                //the 2/2 component can be either a variant or country
                variant = variants
                    .SingleOrDefault(v => v.cultureInfoVariant.Equals(components[1], StringComparison.OrdinalIgnoreCase))
                    .getTextVariant;
                if (variant == null)
                {
                    country = components[1];
                }
            }
            if (components.Length == 3)
            {
                variant = components[1];
                country = components[2];
            }

            string result = language;
            if (country != null) result = $"{result}_{country}";
            if (variant != null) result = $"{result}@{variant}";
            return result;
        }
    }
}
