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

        //TODO: we could also add a method to choose, for instance, en if we pass in en-us, or vice-versa.
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
        //TODO: when extracting, add a comment for real format.
        //while we could hypothetically maintain the actual format and do some magic to swap numbers back in,
        //it would be nice to have a more universally compatible po file (granted we do non-standard plurals)
        //TODO: perhaps we could try to generate plural forms that, while not equivalent, are close.
        [LocalizationMethod]
        public string T(FormattableString id) => T(context: null, id: new StringHelper(id));
        [LocalizationMethod]
        public string T(string context, FormattableString id) => T(context, new StringHelper(id));
        private string T(string context, StringHelper id) => id.GetEntryValue(
            GetSelectedLocale(),
            FindEntry(id.GetUnformattedValue(), context: context)?.Id);

        //for plural methods, aside from these big ones, we dont use optional parameters
        //they make overloading weird and hard to reason about.
        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            RawString specialZero = default,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default)
            => T(
                context: default,
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
                specialZero: new StringHelper(specialZero),
                zero: new StringHelper(zero),
                two: new StringHelper(two),
                few: new StringHelper(few),
                many: new StringHelper(many));
        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString specialZero = default,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default)
            => T(
                context: default,
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
                specialZero: new StringHelper(specialZero),
                zero: new StringHelper(zero),
                two: new StringHelper(two),
                few: new StringHelper(few),
                many: new StringHelper(many));
        //we need these special specialZero overloads to help with overload resolution issues
        //resulting from different specialZero types
        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            RawString specialZero)
            => T(
                context: default,
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
                specialZero: new StringHelper(specialZero),
                zero: default,
                two: default,
                few: default,
                many: default);
        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString specialZero)
            => T(
                context: default,
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
                specialZero: new StringHelper(specialZero),
                zero: default,
                two: default,
                few: default,
                many: default);
        [LocalizationMethod]
        public string T(
            string context,
            FormattableString id,
            decimal count,
            FormattableString other,
            RawString specialZero = default,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default)
            => T(
                context: context,
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
                specialZero: new StringHelper(specialZero),
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
            FormattableString specialZero = default,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default)
            => T(
                context: context,
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
                specialZero: new StringHelper(specialZero),
                zero: new StringHelper(zero),
                two: new StringHelper(two),
                few: new StringHelper(few),
                many: new StringHelper(many));
        //we need these special specialZero overloads to help with overload resolution issues
        //resulting from different specialZero types
        [LocalizationMethod]
        public string T(
            string context,
            FormattableString id,
            decimal count,
            FormattableString other,
            RawString specialZero)
            => T(
                context: context,
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
                specialZero: new StringHelper(specialZero),
                zero: default,
                two: default,
                few: default,
                many: default);
        [LocalizationMethod]
        public string T(
            string context,
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString specialZero)
            => T(
                context: context,
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(other),
                specialZero: new StringHelper(specialZero),
                zero: default,
                two: default,
                few: default,
                many: default);
        //PluralStringHelper and these private methods give us a common and performant common implementation
        //the public methods contain a mix of normal strings and formattable strings to cover the expected
        //cases of formattability performantly
        private string T(
            string context,
            StringHelper id,
            decimal count,
            StringHelper other,
            StringHelper specialZero,
            StringHelper zero,
            StringHelper two,
            StringHelper few,
            StringHelper many)
        {
            Locale selectedLocale = GetSelectedLocale();
            PluralType pluralType = selectedLocale.PluralRules.Evaluate(count, out int index);
            bool eligibleForSpecialZero = count == 0m && selectedLocale.UseSpecialZero;

            string idValue = id.GetUnformattedValue();
            GetTextEntry entry = FindEntry(id: idValue, context: context);

            string targetEntryString;
            StringHelper targetStringHelper;

            if (eligibleForSpecialZero
                //if a po file is marked with special zero, there can be an additional, optional plural value for special zero
                //this line also serves as a null check on entry
                && entry?.PluralValues?.Count == selectedLocale.PluralRules.Count + 1
                && entry.PluralValues[entry.PluralValues.Count - 1] is string specialZeroValue
                && !string.IsNullOrEmpty(specialZeroValue))
            {
                targetEntryString = specialZeroValue;
                targetStringHelper = specialZero;
            }
            else if (pluralType == PluralType.One)
            {
                targetEntryString = entry?.Id;
                targetStringHelper = id;
            }
            else if (entry?.PluralValues?.Count is int totalPluralValues
                && index >= totalPluralValues)
            {
                //TODO: need a bit more thought on this, since generally prefer not to throw when translating
                //because it may be on a hot path that really shouldn't disrupt things
                //one options is to validate and throw on load -- fail fast and up front
                //another options is to return the error as a string here
                //another options is to not throw on load but expose the success or failure as a result
                //will probably do options 2+3, allowing unit tests to work, result to be logged, and not throw anywhere
                //for this low-pri hot-path code.
                //TODO: also audit exceptions as a whole for this project
                throw new InvalidOperationException(
                    $"Cannot get plural value '{index} ({pluralType})' because only {entry.PluralValues.Count} " +
                    $"plural values are provided for entry '{idValue}'.");
            }
            else
            {
                targetEntryString = entry?.PluralValues?[index];
                switch (pluralType)
                {
                    //for this overload, these are normal strings
                    case PluralType.Zero:
                        targetStringHelper = zero;
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
                    case PluralType.One:
                        //should not happen given condition above
                        throw new InvalidOperationException("Hit one plural form even though we have a separate hanlding for it here.");
                    default:
                        //should not realistically happen, but silences static code analysis
                        throw new InvalidOperationException("Hit an unknown plural form");
                }
            }

            string result = targetStringHelper.GetEntryValue(selectedLocale, targetEntryString);
            return result;
        }

        //in our implementation, we dont consider the plural id an actual id
        //mainly because we dont have an explicit plural id because we accept multiple plural forms
        private GetTextEntry FindEntry(string id, string context = null) => selectedDocument
            ?.Entries
            ?.SingleOrDefault(e => e.Context == context && e.Id == id);

        private Locale GetSelectedLocale() => selectedDocument?.Header.Locale ?? nativeLocale;

        private readonly ref struct StringHelper
        {
            private readonly RawString rawString;
            private readonly FormattableString formattableString;

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

            public string GetEntryValue(Locale selectedLocale, string entryString)
            {
                if (entryString == null)
                {
                    return rawString.Value is string rawStringValue
                        ? rawStringValue
                        : formattableString.ToString(selectedLocale.CultureInfo);
                }

                return formattableString == null
                    ? entryString
                    : string.Format(selectedLocale.CultureInfo, entryString, formattableString.GetArguments());
            }
        }
    }
}