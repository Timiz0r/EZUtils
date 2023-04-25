namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using static UnityEngine.EventSystems.EventTrigger;

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
        public GetTextEntry UnderlyingEntry { get; }
        public Locale Locale { get; }

        public GetTextHeader(GetTextEntry underlyingEntry)
        {
            UnderlyingEntry = underlyingEntry;

            Match languageMatch = Regex.Match(
                underlyingEntry.Value,
                "(?i)Language: (?'lang'[a-z]+)(?:_(?'country'[a-z]+))?(?:@(?'variant'[a-z]+))?");
            CultureInfo cultureInfo = languageMatch.Success
                ? GetCultureInfoFromGetTextLanguage(
                    language: languageMatch.Groups["lang"].Value,
                    country: languageMatch.Groups["country"].Value,
                    variant: languageMatch.Groups["variant"].Value)
                //is basically the most important field
                : throw new InvalidOperationException($"Language field not found.");

            PluralRules pluralRules = new PluralRules(
                zero: GetPluralRule("Zero"),
                one: GetPluralRule("One"),
                two: GetPluralRule("Two"),
                few: GetPluralRule("Few"),
                many: GetPluralRule("Many"),
                other: GetPluralRule("Other"));

            Locale = new Locale(cultureInfo, pluralRules);

            string GetPluralRule(string kind) => Regex.Match(
                underlyingEntry.Value,
                $@"(?i)X-PluralRules-{kind}: *([^\n]*)") is Match pluralRuleMatch && pluralRuleMatch.Success
                    ? pluralRuleMatch.Groups[1].Value
                    : null;
        }

        //for cases where we generate an entry, we'll prefer this kind of ctor
        private GetTextHeader(Locale locale, GetTextEntry generatedEntry)
        {
            Locale = locale;
            UnderlyingEntry = generatedEntry;
        }

        //we dont currently allow custom plural forms or charsets
        //and otherwise have no other potentially useful data atm

        public static GetTextHeader ForLocale(Locale locale)
        {
            StringBuilder sb = new StringBuilder();
            string language = GetGetTextLanguageFromCultureInfo(locale.CultureInfo);
            _ = sb.Append($"Language: {language}\n");
            _ = sb.Append("MIME-Version: 1.0\n");
            _ = sb.Append("Content-Type: text/plain; charset=UTF-8\n");
            _ = sb.Append("Content-Transfer-Encoding: 8bit\n");

            //we generate these so that poedit or other tools can see the plural strings property
            int pluralFormCount = locale.PluralRules.Count;
            _ = sb.Append($"Plural-Forms: nplurals={pluralFormCount}; ");
            for (int i = 1; i < pluralFormCount; i++)
            {
                _ = sb.Append($"n == {i} ? {i} : ");
            }
            _ = sb.Append("0;\n");

            if (locale.PluralRules.Zero is string zero) _ = sb.Append($"X-PluralRules-Zero: {zero}\n");
            if (locale.PluralRules.One is string one) _ = sb.Append($"X-PluralRules-One: {one}\n");
            if (locale.PluralRules.Two is string two) _ = sb.Append($"X-PluralRules-Two: {two}\n");
            if (locale.PluralRules.Few is string few) _ = sb.Append($"X-PluralRules-Few: {few}\n");
            if (locale.PluralRules.Many is string many) _ = sb.Append($"X-PluralRules-Many: {many}\n");
            if (locale.PluralRules.Other is string other) _ = sb.Append($"X-PluralRules-Other: {other}\n");

            GetTextEntry entry = new GetTextEntryBuilder()
                .ConfigureId(string.Empty)
                .ConfigureValue(sb.ToString())
                .Create();
            return new GetTextHeader(locale, generatedEntry: entry);
        }


        public GetTextHeader WithLocale(Locale locale)
        {
            List<GetTextLine> underlyingLines = UnderlyingEntry.Lines.ToList();

            //while not every ToEntry line is related to locale, at least at time of writing, it's fine
            ReplacePluralRule("Zero", locale.PluralRules.Zero);
            ReplacePluralRule("One", locale.PluralRules.One);
            ReplacePluralRule("Two", locale.PluralRules.Two);
            ReplacePluralRule("Few", locale.PluralRules.Few);
            ReplacePluralRule("Many", locale.PluralRules.Many);
            ReplacePluralRule("Other", locale.PluralRules.Other);
            underlyingLines.Sort(new PluralRuleLineSorter(underlyingLines));

            GetTextEntry newEntry = GetTextEntry.Parse(underlyingLines);

            return new GetTextHeader(locale: locale, generatedEntry: newEntry);

            void ReplacePluralRule(string ruleName, string rule)
            {
                string headerKey = $"X-PluralRules-{ruleName}:";
                int targetIndex = underlyingLines.FindIndex(
                    l => l.StringValue?.Value?.StartsWith(headerKey, StringComparison.Ordinal) == true);

                if (rule == null)
                {
                    if (targetIndex > -1)
                    {
                        underlyingLines.RemoveAt(targetIndex);
                    }
                    return;
                }

                GetTextLine ruleLine = new GetTextLine(stringValue: GetTextString.FromValue($"{headerKey} {rule}\n"));
                if (targetIndex == -1)
                {
                    int lastPluralRuleIndex = underlyingLines.FindLastIndex(
                        l => l.StringValue.Value.StartsWith("X-PluralRules-"));
                    int insertionPoint = lastPluralRuleIndex == -1 ? underlyingLines.Count : lastPluralRuleIndex + 1;
                    underlyingLines.Insert(insertionPoint, ruleLine);
                }
                else
                {
                    underlyingLines[targetIndex] = ruleLine;
                }
            }
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

        private class PluralRuleLineSorter : IComparer<GetTextLine>
        {
            private static readonly string[] ruleKinds = new[] { "Zero", "One", "Two", "Few", "Many", "Other" };
            private readonly GetTextLine[] underlyingCollection;

            public PluralRuleLineSorter(IEnumerable<GetTextLine> underlyingCollection)
            {
                this.underlyingCollection = underlyingCollection.ToArray();
            }

            public int Compare(GetTextLine x, GetTextLine y)
            {
                if (x.StringValue?.Value.StartsWith("X-PluralRules-", StringComparison.Ordinal) != true
                    || y.StringValue?.Value.StartsWith("X-PluralRules-", StringComparison.Ordinal) != true)
                {
                    int xIndex = Array.IndexOf(underlyingCollection, x);
                    int yIndex = Array.IndexOf(underlyingCollection, y);
                    return xIndex - yIndex;
                }

                int xRuleKindIndex = GetRuleKindIndex(x);
                int yRuleKindIndex = GetRuleKindIndex(y);
                return xRuleKindIndex - yRuleKindIndex;

                int GetRuleKindIndex(GetTextLine line) => Array.FindIndex(
                    ruleKinds,
                    rk => rk == line.StringValue.Value.Substring(
                        "X-PluralRules-".Length,
                        line.StringValue.Value.IndexOf(':') - "X-PluralRules-".Length));
            }
        }
    }
}
