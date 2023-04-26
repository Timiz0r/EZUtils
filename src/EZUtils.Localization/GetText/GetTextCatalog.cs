namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    //TODO: not in the habit of doc comments, but probably want one here and in EZLocalization
    public class GetTextCatalog
    {
        private readonly IReadOnlyList<GetTextDocument> documents;
        private readonly HashSet<Locale> supportedLocales;
        private readonly Locale nativeLocale;
        private GetTextDocument selectedDocument = null;

        public GetTextCatalog(IReadOnlyList<GetTextDocument> documents, Locale nativeLocale)
        {
            this.documents = documents;
            this.nativeLocale = nativeLocale;

            supportedLocales = new HashSet<Locale>(documents.Select(d => d.Header.Locale).Append(nativeLocale));
        }

        public bool Supports(Locale locale) => supportedLocales.Contains(locale);

        public void SelectLocale(Locale locale) => selectedDocument =
            locale == nativeLocale
                ? null
                : documents.SingleOrDefault(d => d.Header.Locale == locale)
                    ?? throw new ArgumentOutOfRangeException(
                        nameof(locale),
                        $"This catalog does not support the locale '{locale}'. " +
                        $"Supported locales: {string.Join(", ", documents.Select(d => d.Header.Locale.CultureInfo))}");
        public Locale SelectLocale(CultureInfo cultureInfo)
        {
            Locale correspondingLocale = supportedLocales.SingleOrDefault(l => l.CultureInfo == cultureInfo)
                ?? throw new ArgumentOutOfRangeException(
                    nameof(cultureInfo),
                    $"This catalog does not support the culture '{cultureInfo}'. " +
                    $"Supported locales: {string.Join(", ", documents.Select(d => d.Header.Locale.CultureInfo))}");
            SelectLocale(correspondingLocale);
            return correspondingLocale;
        }

        //NOTE: we could also add a method to choose, for instance, en if we pass in en-us, or vice-versa.
        //but not important at time of writing
        public Locale SelectLocaleOrNative(params Locale[] locales)
        {
            foreach (Locale locale in locales)
            {
                if (Supports(locale))
                {
                    SelectLocale(locale);
                    return locale;
                }
            }
            SelectLocale(nativeLocale);
            return nativeLocale;
        }
        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
        {
            foreach (CultureInfo cultureInfo in cultureInfos)
            {
                Locale correspondingLocale = supportedLocales.SingleOrDefault(l => l.CultureInfo == cultureInfo);
                if (correspondingLocale != null)
                {
                    SelectLocale(correspondingLocale);
                    return correspondingLocale;
                }
            }
            SelectLocale(nativeLocale);
            return nativeLocale;
        }

        [LocalizationMethod]
        public string T(RawString id) => T(context: null, id: id);
        [LocalizationMethod]
        public string T(string context, RawString id)
            => selectedDocument?.FindEntry(context: context, id: id.Value)?.Value ?? id.Value;
        [LocalizationMethod]
        public string T(FormattableString id) => T(context: null, id: id);
        [LocalizationMethod]
        public string T(string context, FormattableString id)
        {
            Locale selectedLocale = GetSelectedLocale();
            GetTextEntry entry = selectedDocument?.FindEntry(context: context, id: id.Format);

            string result = entry == null
                ? id.ToString(selectedLocale.CultureInfo)
                : string.Format(selectedLocale.CultureInfo, entry.Value, id.GetArguments());
            return result;
        }

        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other)
            => T(
                context: default,
                id: id,
                count: count,
                other: other,
                zero: default,
                two: default,
                few: default,
                many: default);
        [LocalizationMethod]
        public string T(
            string context,
            FormattableString id,
            decimal count,
            FormattableString other)
            => T(
                context: context,
                id: id,
                count: count,
                other: other,
                zero: default,
                two: default,
                few: default,
                many: default);
        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default)
            => T(
                context: default,
                id: id,
                count: count,
                other: other,
                zero: zero,
                two: two,
                few: few,
                many: many);
        [LocalizationMethod]
        public string T(
            string context,
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default)
        {
            Locale selectedLocale = GetSelectedLocale();
            PluralType pluralType = selectedLocale.PluralRules.Evaluate(count, out int index);

            GetTextEntry entry = selectedDocument?.FindEntry(context: context, id: id.Format);

            if (entry == null
                //it's hard to know what to do if the counts dont match since we cant really know which one to pick
                //so we force it to use native language
                || entry.PluralValues.Count != selectedLocale.PluralRules.Count)
            {
                FormattableString targetFormattableString;
                switch (pluralType)
                {
                    //for this overload, these are normal strings
                    case PluralType.Zero:
                        targetFormattableString = zero;
                        break;
                    case PluralType.One:
                        targetFormattableString = id;
                        break;
                    case PluralType.Two:
                        targetFormattableString = two;
                        break;
                    case PluralType.Few:
                        targetFormattableString = few;
                        break;
                    case PluralType.Many:
                        targetFormattableString = many;
                        break;
                    case PluralType.Other:
                        targetFormattableString = other;
                        break;
                    default:
                        //should not realistically happen, but silences static code analysis
                        throw new InvalidOperationException("Hit an unknown plural form");
                }
                if (targetFormattableString == null)
                {
                    targetFormattableString = other;
                }

                string nativeResult = targetFormattableString.ToString(selectedLocale.CultureInfo);
                return nativeResult;
            }

            string entryFormat = entry.PluralValues[index];
            string result = string.Format(selectedLocale.CultureInfo, entryFormat, other.GetArguments());
            return result;
        }

        private Locale GetSelectedLocale() => selectedDocument?.Header.Locale ?? nativeLocale;
    }
}
