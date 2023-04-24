namespace EZUtils.Localization.Proxy
{
    using System;
    using System.Globalization;
    using EZUtils.Localization;
    using UnityEditor;
    using UnityEngine.UIElements;

    //ports-and-adapters-wise, EZLocalization is a driver adapter connecting unity editor to a catalog
    [LocalizationProxy]
    [GenerateLanguage("ja", "ja.po", UseSpecialZero = true, Other = " @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")]
    [GenerateLanguage("ko", "ko.po", UseSpecialZero = true, Other = " @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")]
    public static class Localization
    {
        private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder("Packages/com.timiz0r.ezutils.localization", "EZUtils");

        public static void SelectLocale(Locale locale) => loc.SelectLocale(locale: locale);
        public static Locale SelectLocale(CultureInfo cultureInfo) => loc.SelectLocale(cultureInfo: cultureInfo);
        public static Locale SelectLocaleOrNative(params Locale[] locales) => loc.SelectLocaleOrNative(locales: locales);
        public static Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos) => loc.SelectLocaleOrNative(cultureInfos: cultureInfos);

        //while we support custom retranslation via IRetranslatable, the most recommended way to support retranslation
        //for elements created in-code is to call EZLocalization.TranslateElementTree for the newly created element.
        //but for non-TextElements and non-BaseField`1s, we have IRetranslatable.
        //TODO: plural implementation? kinda hard to do since this is recursive.
        //leaving this implemention for when we decide how the numeric value gets updated
        //perhaps we need to exclude plural ones in the recursive one, and have the plural overload not be recursive
        //still gotta decide how to get the value inside. easiest is {0}
        //but ofc we have plural forms to consider, so...
        //locplural:one/id:type2:type3:type4
        //and which one each maps to depends on native locale
        //and will need to escape : with ::, ofc only if a localplural: prefix
        public static void TranslateElementTree(VisualElement rootElement) => loc.TranslateElementTree(rootElement: rootElement);
        [LocalizationMethod]
        public static void TranslateWindowTitle(
            EditorWindow window,
            [LocalizationParameter(LocalizationParameter.Id)] string titleText) => loc.TranslateWindowTitle(window: window, titleText: titleText);

        [LocalizationMethod]
        public static string T(RawString id) => loc.T(id: id);
        [LocalizationMethod]
        public static string T(FormattableString id) => loc.T(id: id);
        [LocalizationMethod]
        public static string T(string context, RawString id) => loc.T(context: context, id: id);
        [LocalizationMethod]
        public static string T(string context, FormattableString id) => loc.T(context: context, id: id);

        [LocalizationMethod]
        public static string T(
            FormattableString id,
            decimal count,
            FormattableString other) => loc.T(id: id, count: count, other: other);
        [LocalizationMethod]
        public static string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            RawString specialZero) => loc.T(id: id, count: count, other: other, specialZero: specialZero);
        [LocalizationMethod]
        public static string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString specialZero) => loc.T(id: id, count: count, other: other, specialZero: specialZero);
        [LocalizationMethod]
        public static string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            RawString specialZero = default,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default) => loc.T(id: id, count: count, other: other, specialZero: specialZero, zero: zero, two: two, few: few, many: many);
        [LocalizationMethod]
        public static string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString specialZero,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default) => loc.T(id: id, count: count, other: other, specialZero: specialZero, zero: zero, two: two, few: few, many: many);
        [LocalizationMethod]
        public static string T(
            string context,
            FormattableString id,
            decimal count,
            FormattableString other,
            RawString specialZero,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default) => loc.T(context: context, id: id, count: count, other: other, specialZero: specialZero, zero: zero, two: two, few: few, many: many);
        [LocalizationMethod]
        public static string T(
            string context,
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString specialZero,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default) => loc.T(context: context, id: id, count: count, other: other, specialZero: specialZero, zero: zero, two: two, few: few, many: many);
        [LocalizationMethod]
        public static string T(
            string context,
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default) => loc.T(context: context, id: id, count: count, other: other, zero: zero, two: two, few: few, many: many);
    }
}
