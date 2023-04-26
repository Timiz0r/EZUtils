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
        public string T(RawString id) => T(context: null, id: new StringHelper(id));
        [LocalizationMethod]
        public string T(string context, RawString id) => T(context, new StringHelper(id));
        [LocalizationMethod]
        public string T(FormattableString id) => T(context: null, id: new StringHelper(id));
        [LocalizationMethod]
        public string T(string context, FormattableString id) => T(context, new StringHelper(id));
        private string T(string context, StringHelper id)
        {
            Locale selectedLocale = GetSelectedLocale();
            GetTextEntry entry = selectedDocument?.FindEntry(context: context, id: id.GetUnformattedValue());

            string result = entry == null
                ? id.GetValue(selectedLocale)
                : id.FormatValue(selectedLocale, entry.Value);
            return result;
        }

        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other)
            => T(
                context: default,
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
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
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
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
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
                zero: new StringHelper(zero),
                two: new StringHelper(two),
                few: new StringHelper(few),
                many: new StringHelper(many));
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
            => T(
                context: context,
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
                zero: new StringHelper(zero),
                two: new StringHelper(two),
                few: new StringHelper(few),
                many: new StringHelper(many));

        //PluralStringHelper and these private methods give us a common and performant common implementation
        //the public methods contain a mix of normal strings and formattable strings to cover the expected
        //cases of formattability performantly
        private string T(
            string context,
            StringHelper id,
            decimal count,
            StringHelper other,
            StringHelper zero,
            StringHelper two,
            StringHelper few,
            StringHelper many)
        {
            Locale selectedLocale = GetSelectedLocale();
            PluralType pluralType = selectedLocale.PluralRules.Evaluate(count, out int index);

            string idValue = id.GetUnformattedValue();
            GetTextEntry entry = selectedDocument?.FindEntry(context: context, id: idValue);

            if (entry == null
                //it's hard to know what to do if the counts dont match since we cant really know which one to pick
                //so we force it to use native language
                || entry.PluralValues.Count != selectedLocale.PluralRules.Count)
            {
                StringHelper targetStringHelper;
                switch (pluralType)
                {
                    //for this overload, these are normal strings
                    case PluralType.Zero:
                        targetStringHelper = zero;
                        break;
                    case PluralType.One:
                        targetStringHelper = id;
                        break;
                    case PluralType.Two:
                        targetStringHelper = two;
                        break;
                    case PluralType.Few:
                        targetStringHelper = few;
                        break;
                    case PluralType.Many:
                        targetStringHelper = many;
                        break;
                    case PluralType.Other:
                        targetStringHelper = other;
                        break;
                    default:
                        //should not realistically happen, but silences static code analysis
                        throw new InvalidOperationException("Hit an unknown plural form");
                }
                if (targetStringHelper.IsEmpty)
                {
                    targetStringHelper = other;
                }

                string nativeResult = targetStringHelper.GetValue(selectedLocale);
                return nativeResult;
            }

            string entryFormat = entry.PluralValues[index];
            string result = other.FormatValue(selectedLocale, entryFormat);
            return result;
        }

        private Locale GetSelectedLocale() => selectedDocument?.Header.Locale ?? nativeLocale;

        private readonly ref struct StringHelper
        {
            private readonly RawString rawString;
            private readonly FormattableString formattableString;

            public bool IsEmpty => rawString.Value == null && formattableString == null;

            public StringHelper(RawString rawString)
            {
                this.rawString = rawString;
                formattableString = null;
            }
            public StringHelper(FormattableString formattableString)
            {
                rawString = default;
                this.formattableString = formattableString;
            }

            public string GetUnformattedValue() => rawString.Value is string rawStringValue
                ? rawStringValue
                : formattableString.Format;

            public string GetValue(Locale selectedLocale) => rawString.Value is string rawStringValue
                ? rawStringValue
                : formattableString.ToString(selectedLocale.CultureInfo);

            public string FormatValue(Locale selectedLocale, string format)
                => formattableString == null
                    ? format
                    : string.Format(selectedLocale.CultureInfo, format, formattableString.GetArguments());
        }
    }
}
