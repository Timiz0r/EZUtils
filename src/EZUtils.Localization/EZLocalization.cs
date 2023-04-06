namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    //ports-and-adapters-wise, EZLocalization is a driver adapter connecting unity editor to a catalog
    public class EZLocalization
    {
        private CatalogReference catalog;
        private LocaleSelector localeSelector;

        private EZLocalization(CatalogReference catalog, LocaleSelector localeSelector)
        {
            this.catalog = catalog;
            this.localeSelector = localeSelector;
        }

        public static EZLocalization ForCatalogUnder(string root, CultureInfo nativeLocale)
        {
            root = root.TrimEnd(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            CatalogReference catalog = CatalogDatabase.GetCatalogReference(root, nativeLocale);
            LocaleSelector localeSelector = LocaleSelector.Create(root, catalog);
            EZLocalization result = new EZLocalization(catalog, localeSelector);
            return result;
        }
        public static EZLocalization ForCatalogUnder(string root)
            => ForCatalogUnder(root, CultureInfo.GetCultureInfo("en"));

        //while we support custom retranslation via IRetranslatable, the most recommended way to support retranslation
        //for elements created in-code is to call EZLocalization.TranslateElementTree for the newly created element.
        //but for non-TextElements and non-BaseField`1s, we have IRetranslatable.
        //TODO: plural implementation? kinda hard to do since this is recursive.
        //perhaps we need to exclude plural ones in the recursive one, and have the plural overload not be recursive
        //still gotta decide how to get the value inside. easiest is {0}
        public void TranslateElementTree(VisualElement rootElement)
            //descendents includes element, as well
            => rootElement.Query().Descendents<VisualElement>().ForEach(element =>
            {
                if (element is IRetranslatable retranslatable)
                {
                    //making this first so that we can override default behavior if necessary
                    retranslatable.Retranslate();
                }
                else if (element is TextElement textElement
                    && textElement.text is string teOriginalValue
                    && teOriginalValue.StartsWith("loc:", StringComparison.Ordinal))
                {
                    teOriginalValue = teOriginalValue.Substring(4);
                    catalog.TrackRetranslatable(textElement, () => textElement.text = T(teOriginalValue));
                }
                else if (element.GetType() is Type elementType
                    && elementType.GetGenericTypeDefinition() == typeof(BaseField<>)
                    && elementType.GetProperty("label", BindingFlags.Public | BindingFlags.Instance) is PropertyInfo labelProperty
                    && labelProperty.GetValue(element) is string lbOriginalValue
                    && lbOriginalValue.StartsWith("loc:", StringComparison.Ordinal))
                {
                    lbOriginalValue = lbOriginalValue.Substring(4);
                    catalog.TrackRetranslatable(element, () => labelProperty.SetValue(element, T(lbOriginalValue)));
                }
            });
        public string T(string id) => catalog.Catalog.T(id);
        public void TranslateWindow(EditorWindow window, string titleText)
            => catalog.TrackRetranslatable(window, () => window.titleContent.text = T(titleText));
    }
}
