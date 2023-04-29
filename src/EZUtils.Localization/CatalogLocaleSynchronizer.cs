namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using UnityEditor;

    //NOTE:
    //this is tightly coupled to catalogreference, kept separate for organization
    //but this tight-coupling makes for intricate behavior, particularly because multiple catalog references get
    //synchronized to the same synchronizer
    //
    //CatalogLocaleSynchronizer.SetLocale is called by CatalogReference (via EZLocalization). its intended purpose
    //is to propagate a locale change from one EZLocalization/CatalogReference instance to others, for the same key.
    //
    //of course, CatalogLocaleSynchronizer.SetLocale can't just call CatalogReference.SetLocale for infinite recursion
    //reasons, hence calling the underlying GetTextCatalog.SetLocale.
    //to delve further, if CatalogReference.Catalog is called first, there is no problem because code is carefuly organized
    //to avoid the cycle. CatalogReference.SelectLocale is a bit tricky, so, over there, we first ensure the catalog
    //is initialized.
    //
    //in the event CatalogReference needs to reload a new GetTextCatalog, it reads CatalogLocaleSynchronizer.SelectedLocale
    //to do it.
    //
    //previous designs had these classes combined, where a CatalogDatabase maintains instances based on sync key and catalog path.
    //this design wasn't sufficient because we want two entirely different catalogs to be able to synchronize to the same locale.
    internal class CatalogLocaleSynchronizer
    {
        private static readonly Dictionary<string, CatalogLocaleSynchronizer> synchronizers =
            new Dictionary<string, CatalogLocaleSynchronizer>();

        private readonly string selectedLocaleEditorPrefKey;
        private readonly List<CatalogReference> subscribers = new List<CatalogReference>();

        public Locale SelectedLocale { get; private set; }

        private CatalogLocaleSynchronizer(string localeSynchronizationKey, Locale initialLocale)
        {
            selectedLocaleEditorPrefKey = $"EZUtils.Localization.SelectedLocale.{localeSynchronizationKey}";
            //for the very first time we initialize this class, there's no stored language
            //so Register's SelectLocaleOrNative passes two nulls
            //our SelectLocaleOrNative does not set a SelectedLocale if the catalog's SelectLocaleOrNative uses native
            //without this, we would try store store a null SelectedLocale and NRE
            //of course, we could instead store nothing until the first user SelectLocale,
            //but getting a setting more immediately seems nicer
            SelectedLocale = initialLocale;
        }

        public static CatalogLocaleSynchronizer Register(string localeSynchronizationKey, CatalogReference catalogReference)
        {
            if (!synchronizers.TryGetValue(localeSynchronizationKey, out CatalogLocaleSynchronizer value))
            {
                synchronizers[localeSynchronizationKey] = value = new CatalogLocaleSynchronizer(
                    localeSynchronizationKey, catalogReference.NativeLocale);

                string prefValue = EditorPrefs.GetString(value.selectedLocaleEditorPrefKey);
                CultureInfo storedLocale = !string.IsNullOrEmpty(prefValue)
                    ? CultureInfo.GetCultureInfo(prefValue)
                    : null;
                //this line twice because, here, the next SelectLocaleOrNative needs a subscriber
                value.subscribers.Add(catalogReference);
                _ = value.SelectLocaleOrNative(UnityLanguageTracking.NewlySelectedUnityEditorLanguage, storedLocale);
            }
            else
            {
                value.subscribers.Add(catalogReference);
            }

            return value;
        }

        public void SelectLocale(Locale locale)
        {
            SelectedLocale = locale;
            foreach (CatalogReference reference in subscribers)
            {
                _ = reference.Catalog.TrySelectLocale(locale);
            }
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
        }
        public Locale SelectLocale(CultureInfo cultureInfo)
        {
            bool foundLocale = false;
            foreach (CatalogReference reference in subscribers)
            {
                if (reference.Catalog.TrySelectLocale(cultureInfo, out Locale correspondingLocale) && !foundLocale)
                {
                    foundLocale = true;
                    SelectedLocale = correspondingLocale;
                }
            }
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            return SelectedLocale;
        }

        public bool TrySelectLocale(Locale locale)
        {
            bool foundLocale = false;
            foreach (CatalogReference reference in subscribers)
            {
                if (reference.Catalog.TrySelectLocale(locale) && !foundLocale)
                {
                    foundLocale = true;
                    SelectedLocale = locale;
                }
            }
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            return foundLocale;
        }
        public bool TrySelectLocale(CultureInfo cultureInfo, out Locale correspondingLocale)
        {
            bool foundLocale = false;
            foreach (CatalogReference reference in subscribers)
            {
                if (reference.Catalog.TrySelectLocale(cultureInfo, out Locale currentReferenceLocale) && !foundLocale)
                {
                    foundLocale = true;
                    SelectedLocale = currentReferenceLocale;
                }
            }
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            correspondingLocale = foundLocale ? SelectedLocale : null;
            return foundLocale;
        }
        public bool TrySelectLocale(CultureInfo cultureInfo) => TrySelectLocale(cultureInfo, out _);

        public Locale SelectLocaleOrNative(params Locale[] locales)
        {
            bool foundLocale = false;
            foreach (CatalogReference reference in subscribers)
            {
                if (reference.Catalog.SelectLocaleOrNative(locales) is Locale selectedLocale
                    && !foundLocale
                    && Array.IndexOf(locales, selectedLocale) > -1) //aka is not native
                {
                    foundLocale = true;
                    SelectedLocale = selectedLocale;
                }
            }
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            return SelectedLocale;
        }
        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
        {
            bool foundLocale = false;
            foreach (CatalogReference reference in subscribers)
            {
                if (reference.Catalog.SelectLocaleOrNative(cultureInfos) is Locale selectedLocale
                    && !foundLocale
                    && Array.Exists(cultureInfos, c => c == selectedLocale.CultureInfo)) //aka is not native
                {
                    foundLocale = true;
                    SelectedLocale = selectedLocale;
                }
            }
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            return SelectedLocale;
        }
        //needed to disambiguate between locale vs cultureinfo params arrays
        public Locale SelectLocaleOrNative() => SelectLocaleOrNative(Array.Empty<Locale>());
    }
}
