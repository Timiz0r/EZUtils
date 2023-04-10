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
        private Locale selectedLocale;

        public GetTextCatalog Catalog { get; private set; }
        public Locale NativeLocale { get; }

        public CatalogReference(Locale nativeLocale)
        {
            NativeLocale = selectedLocale = nativeLocale;
        }

        public void UseUpdatedCatalog(GetTextCatalog catalog)
        {
            Catalog = catalog;
            Catalog.SelectLocale(selectedLocale);
        }

        public void SelectLocale(Locale locale)
        {
            selectedLocale = locale;
            Catalog.SelectLocale(locale);
        }
        public Locale SelectLocale(CultureInfo cultureInfo) => selectedLocale = Catalog.SelectLocale(cultureInfo);
        public Locale SelectLocaleOrNative(params Locale[] locales)
            => selectedLocale = Catalog.SelectLocaleOrNative(locales);
        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
            => selectedLocale = Catalog.SelectLocaleOrNative(cultureInfos);

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
