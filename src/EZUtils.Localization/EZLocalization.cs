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
        private readonly List<(UnityEngine.Object obj, Action action)> retranslatableObjects =
            new List<(UnityEngine.Object obj, Action action)>();
        private readonly List<(VisualElement element, Action action)> retranslatableElements =
            new List<(VisualElement element, Action action)>();

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

        //TODO: remove locale selection; will actually eventually replace with something that returns a control capable of selecting this instance's locale
        //the retranslation stuff will probably integrate in some way into locale selector
        public void Test(CultureInfo locale)
        {
            localeSelector.SelectLocale(locale);

            _ = retranslatableObjects.RemoveAll(t => t.obj == null); //aka destroyed
            foreach ((_, Action action) in retranslatableObjects)
            {
                action();
            }

            _ = retranslatableElements.RemoveAll(t => t.element.panel == null); //aka removed from hierarchy
            foreach ((_, Action action) in retranslatableElements)
            {
                action();
            }
        }

        //TODO: support custom elements. the hard design part is supporting elements with many things to translate
        //probably via a single-method interface that gets called
        //for the edge case where we've added an element that uses a separate catalog, it should be fine as long
        //as the parent was translated before the child was added. we recommend translating right after cloning,
        //so this shouldn't be a problem in practice. note that we don't have a special construct for cloning
        //visual asset trees only, because we want to support translating elements that dont come from uxml.
        //TODO: plural implementation? kinda hard to do since this is recursive.
        //perhaps we need to exclude plural ones in the recursive one, and have the plural overload not be recursive
        //still gotta decide how to get the value inside. easiest is {0}
        public void TranslateElementTree(VisualElement rootElement)
        {
            //descendents includes element, as well
            foreach (VisualElement element in rootElement.Query().Descendents<VisualElement>().ToList())
            {
                if (element is TextElement textElement
                    && textElement.text is string teOriginalValue
                    && teOriginalValue.StartsWith("loc:", StringComparison.Ordinal))
                {
                    teOriginalValue = teOriginalValue.Substring(4);
                    TrackRetranslatable(textElement, () => textElement.text = T(teOriginalValue));
                }
                else if (element.GetType().GetProperty("label", BindingFlags.Public | BindingFlags.Instance) is PropertyInfo labelProperty
                    && labelProperty.GetValue(element) is string lbOriginalValue
                    && lbOriginalValue.StartsWith("loc:", StringComparison.Ordinal)
                    && labelProperty.CanWrite)
                {
                    lbOriginalValue = lbOriginalValue.Substring(4);
                    TrackRetranslatable(element, () => labelProperty.SetValue(element, T(lbOriginalValue)));
                }
            }
        }
        public string T(string id) => catalog.Catalog.T(id);
        public void TranslateWindow(EditorWindow window, string titleText)
            => TrackRetranslatable(window, () => window.titleContent.text = T(titleText));

        private void TrackRetranslatable(UnityEngine.Object obj, Action action)
        {
            retranslatableObjects.Add((obj, action));
            action();
        }
        private void TrackRetranslatable(VisualElement element, Action action)
        {
            retranslatableElements.Add((element, action));
            action();
        }
    }
}
