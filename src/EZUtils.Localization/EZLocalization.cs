namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using UnityEditor;
    using UnityEngine.UIElements;

    //ports-and-adapters-wise, EZLocalization is a driver adapter connecting unity editor to a catalog
    public class EZLocalization
    {
        private CatalogReference catalog;
        private LocaleSelector localeSelector;
        private readonly List<(UnityEngine.Object obj, Action action)> retranslatableObjects =
            new List<(UnityEngine.Object obj, Action action)>();

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
            // _ = retranslatableObjects.RemoveAll(t => t.obj == null); //aka destroyed
            // foreach ((_, Action action) in retranslatableObjects)
            // {
            //     action();
            // }
        }

        public void T(VisualElement rootVisualElement) => throw new NotImplementedException();
        public string T(string id) => catalog.Catalog.T(id);
        public void T(EditorWindow window, string id)
            => TrackRetranslatable(window, () => window.titleContent.text = T(id));

        private void TrackRetranslatable(UnityEngine.Object obj, Action action)
        {
            retranslatableObjects.Add((obj, action));
            action();
        }
    }
}
