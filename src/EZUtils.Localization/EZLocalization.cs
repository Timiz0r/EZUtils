namespace EZUtils.Localization
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine.UIElements;

    //ports-and-adapters-wise, EZLocalization is a driver adapter connecting unity editor to a catalog
    public class EZLocalization
    {
        private readonly CatalogReference catalogReference;

        private EZLocalization(CatalogReference catalogReference)
        {
            this.catalogReference = catalogReference;
        }

        public static EZLocalization ForCatalogUnder(string root, string localeSynchronizationKey, Locale nativeLocale)
        {
            root = root.Trim(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

            CatalogReference catalog = new CatalogReference(
                root: root,
                nativeLocale: nativeLocale,
                localeSynchronizationKey: localeSynchronizationKey);
            EZLocalization result = new EZLocalization(catalog);

            return result;
        }
        public static EZLocalization ForCatalogUnder(string root, string localeSynchronizationKey)
            => ForCatalogUnder(root, localeSynchronizationKey, Locale.English);

        //TODO: dont think we still have quite the right behavior, where we want the pref to be editor wide?
        public void SelectLocale(Locale locale) => catalogReference.SelectLocale(locale);
        public Locale SelectLocale(CultureInfo cultureInfo) => catalogReference.SelectLocale(cultureInfo);
        public Locale SelectLocaleOrNative(params Locale[] locales)
            => _ = catalogReference.SelectLocaleOrNative(locales);
        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
            => _ = catalogReference.SelectLocaleOrNative(cultureInfos);

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
        public void TranslateElementTree(VisualElement rootElement)
            //descendents includes element, as well
            => rootElement.Query().Descendents<VisualElement>().ForEach(element =>
            {
                if (element is IRetranslatableElement retranslatable)
                {
                    //making this first so that we can override default behavior if necessary
                    retranslatable.Retranslate();
                }
                else if (element is TextElement textElement
                    && textElement.text is string teOriginalValue
                    && teOriginalValue.StartsWith("loc:", StringComparison.Ordinal))
                {
                    teOriginalValue = teOriginalValue.Substring(4);
                    catalogReference.TrackRetranslatable(textElement, () => textElement.text = T(teOriginalValue));
                }
                else if (element.GetType() is Type elementType
                    && InheritsFromGenericType(elementType.GetType(), typeof(BaseField<>))
                    && elementType.GetProperty("label", BindingFlags.Public | BindingFlags.Instance) is PropertyInfo labelProperty
                    && labelProperty.GetValue(element) is string lbOriginalValue
                    && lbOriginalValue.StartsWith("loc:", StringComparison.Ordinal))
                {
                    lbOriginalValue = lbOriginalValue.Substring(4);
                    catalogReference.TrackRetranslatable(element, () => labelProperty.SetValue(element, T(lbOriginalValue)));
                }
            });
        [LocalizationMethod]
        public void TranslateWindowTitle(
            EditorWindow window,
            [LocalizationParameter(LocalizationParameter.Id)] string titleText)
            => catalogReference.TrackRetranslatable(window, () => window.titleContent.text = T(titleText));

        [LocalizationMethod]
        public string T(RawString id) => catalogReference.Catalog.T(id);
        [LocalizationMethod]
        public string T(FormattableString id) => catalogReference.Catalog.T(id);
        [LocalizationMethod]
        public string T(string context, RawString id) => catalogReference.Catalog.T(context, id);
        [LocalizationMethod]
        public string T(string context, FormattableString id) => catalogReference.Catalog.T(context, id);

        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            RawString specialZero) => catalogReference.Catalog.T(
                id: id,
                count: count,
                other: other,
                specialZero: specialZero,
                zero: default,
                two: default,
                few: default,
                many: default);
        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString specialZero) => catalogReference.Catalog.T(
                id: id,
                count: count,
                other: other,
                specialZero: specialZero,
                zero: default,
                two: default,
                few: default,
                many: default);
        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            RawString specialZero = default,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default) => catalogReference.Catalog.T(
                id: id,
                count: count,
                other: other,
                zero: zero,
                specialZero: specialZero,
                two: two,
                few: few,
                many: many);
        [LocalizationMethod]
        public string T(
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString specialZero,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default) => catalogReference.Catalog.T(
                id: id,
                count: count,
                other: other,
                zero: zero,
                specialZero: specialZero,
                two: two,
                few: few,
                many: many);
        [LocalizationMethod]
        public string T(
            string context,
            FormattableString id,
            decimal count,
            FormattableString other,
            RawString specialZero,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default) => catalogReference.Catalog.T(
                context: context,
                id: id,
                count: count,
                other: other,
                zero: zero,
                specialZero: specialZero,
                two: two,
                few: few,
                many: many);
        [LocalizationMethod]
        public string T(
            string context,
            FormattableString id,
            decimal count,
            FormattableString other,
            FormattableString specialZero,
            FormattableString zero = default,
            FormattableString two = default,
            FormattableString few = default,
            FormattableString many = default) => catalogReference.Catalog.T(
                context: context,
                id: id,
                count: count,
                other: other,
                specialZero: specialZero,
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
            FormattableString many = default) => catalogReference.Catalog.T(
                context: context,
                id: id,
                count: count,
                other: other,
                specialZero: default,
                zero: zero,
                two: two,
                few: few,
                many: many);

        private static bool InheritsFromGenericType(Type typeToInspect, Type genericTypeDefinition)
        {
            if (typeToInspect == null) return false;
            if (typeToInspect.IsGenericType
                && typeToInspect.GetGenericTypeDefinition() is Type genericType
                && genericType == genericTypeDefinition) return true;
            return InheritsFromGenericType(typeToInspect.BaseType, genericTypeDefinition);
        }
    }
}
