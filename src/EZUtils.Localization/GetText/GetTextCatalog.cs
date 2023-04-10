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

        public string T(RawString id) => T(new StringHelper(id));
        //TODO: when extracting, add a comment for real format.
        //while we could hypothetically maintain the actual format and do some magic to swap numbers back in,
        //it would be nice to have a more universally compatible po file (granted we do non-standard plurals)
        //TODO: perhaps we could try to generate plural rules that, while not equivalent, are close.
        public string T(FormattableString id) => T(new StringHelper(id));
        private string T(StringHelper id) => id.GetEntryValue(
            GetSelectedLocale(),
            FindEntry(id.GetUnformattedValue())?.Id);

        //for plural methods, aside from these big ones, we dont use optional parameters
        //they make overloading weird and hard to reason about.
        public string T(
            RawString id,
            decimal count,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default,
            FormattableString other = default,
            RawString specialZero = default)
            => T(
                id: new StringHelper(id),
                count: count,
                zero: new StringHelper(zero),
                two: new StringHelper(two),
                few: new StringHelper(few),
                many: new StringHelper(many),
                other: new StringHelper(other),
                specialZero: new StringHelper(specialZero));
        public string T(
            FormattableString id,
            decimal count,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default,
            FormattableString other = default,
            FormattableString specialZero = default)
            => T(
                id: new StringHelper(id),
                count: count,
                zero: new StringHelper(zero),
                two: new StringHelper(two),
                few: new StringHelper(few),
                many: new StringHelper(many),
                other: new StringHelper(other),
                specialZero: new StringHelper(specialZero));
        //design-wise, one thought was to not allow these, since we expect at least one other plural form
        //for (native) languages with just one form (other), you'd typically provide the same value in id and plural
        //but this is convenient, so keeping it, even if it may actually be user error, since english is the usual
        //native language.
        //if we could keep this from compiling, we might actually choose to disallow them, but we can't stop overload
        //resolution from going to the method with many default parameters
        public string T(
            FormattableString otherPluralForm,
            decimal count)
            => T(
                //note that since such languages only have other and not one, id's string will never be returned
                id: new StringHelper(otherPluralForm),
                count: count,
                other: new StringHelper(otherPluralForm));
        //we dont have special zero overloads for single plural form method(s) because one will conflict with the
        //two plural form overloads, which will be more commonly used.

        //since english is the most common native language, we provide compatible overloads
        //which means two plural forms: one and other
        public string T(
            RawString id,
            decimal count,
            FormattableString otherPluralForm,
            RawString specialZero)
            => T(
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(otherPluralForm),
                specialZero: new StringHelper(specialZero));
        public string T(
            FormattableString id,
            decimal count,
            FormattableString otherPluralForm,
            FormattableString specialZero)
            => T(
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(otherPluralForm),
                specialZero: new StringHelper(specialZero));
        public string T(
            RawString id,
            decimal count,
            FormattableString otherPluralForm)
            => T(
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(otherPluralForm));
        public string T(
            FormattableString id,
            decimal count,
            FormattableString otherPluralForm)
            => T(
                id: new StringHelper(id),
                count: count,
                other: new StringHelper(otherPluralForm));

        //PluralStringHelper and these private methods give us a common and performant common implementation
        //the public methods contain a mix of normal strings and formattable strings to cover the expected
        //cases of formattability performantly
        private string T(
            StringHelper id,
            decimal count,
            StringHelper zero = default,
            StringHelper two = default,
            StringHelper few = default,
            StringHelper many = default,
            StringHelper other = default,
            StringHelper specialZero = default)
        {
            Locale selectedLocale = GetSelectedLocale();
            PluralType pluralType = selectedLocale.PluralRules.Evaluate(count, out int index);

            GetTextEntry entry = FindEntry(id.GetUnformattedValue());
            string targetEntryString;
            StringHelper targetStringHelper;
            if (count == 0m && selectedLocale.UseSpecialZero)
            {
                //if a po file is marked with special zero, we specify that the last plural value is the special zero one
                targetEntryString = entry.PluralValues[entry.PluralValues.Count - 1];
                targetStringHelper = specialZero;
            }
            else if (pluralType != PluralType.One)
            {
                targetEntryString = entry.PluralValues[index];
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
                        throw new InvalidOperationException("Hit one plural form even though we have a separate hanlding for it here.");
                    default:
                        //should not realistically happen
                        throw new InvalidOperationException("Hit an unknown plural form");
                }
            }
            else
            {
                targetEntryString = entry.Id;
                targetStringHelper = id;
            }

            string result = targetStringHelper.GetEntryValue(selectedLocale, targetEntryString);
            return result;
        }

        private GetTextEntry FindEntry(string id) => selectedDocument
            ?.Entries
            ?.SingleOrDefault(e => e.Context == null && e.PluralId == null && e.Id == id);

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
