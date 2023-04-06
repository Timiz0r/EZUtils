namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using UnityEngine.UIElements;

    public class CatalogReference
    {
        private readonly List<(UnityEngine.Object obj, Action action)> retranslatableObjects =
            new List<(UnityEngine.Object obj, Action action)>();
        private readonly List<(VisualElement element, Action action)> retranslatableElements =
            new List<(VisualElement element, Action action)>();
        private CultureInfo selectedLocale;

        public GetTextCatalog Catalog { get; private set; }
        public CultureInfo NativeLocale { get; }

        public CatalogReference(CultureInfo nativeLocale)
        {
            NativeLocale = selectedLocale = nativeLocale;
        }

        public void UseUpdatedCatalog(GetTextCatalog catalog)
        {
            Catalog = catalog;
            Catalog.SelectLocale(selectedLocale);
        }

        public void SelectLocale(CultureInfo locale)
        {
            selectedLocale = locale;
            Catalog.SelectLocale(locale);
        }

        public void Retranslate()
        {
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
        public void TrackRetranslatable(UnityEngine.Object obj, Action action)
        {
            retranslatableObjects.Add((obj, action));
            action();
        }
        public void TrackRetranslatable(VisualElement element, Action action)
        {
            retranslatableElements.Add((element, action));
            action();
        }
    }
}
