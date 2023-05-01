namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using EZUtils.Localization.UIElements;
    using UnityEditor;

    internal class CatalogLocaleSynchronizer
    {
        private static readonly Dictionary<string, CatalogLocaleSynchronizer> synchronizers =
            new Dictionary<string, CatalogLocaleSynchronizer>();

        private readonly string selectedLocaleEditorPrefKey;
        private readonly List<CatalogReference> subscribers = new List<CatalogReference>();

        public Locale SelectedLocale { get; private set; }

        public IReadOnlyList<Locale> SupportedLocales => subscribers
            .SelectMany(cr => cr.SupportedLocales)
            //if a subscriber hasn't been added yet, we still want at least the initial locale to show up
            .Append(SelectedLocale)
            .Distinct()
            .ToArray();

        public LocaleSelectionUI UI { get; }


        private CatalogLocaleSynchronizer(string localeSynchronizationKey, Locale initialLocale)
        {
            selectedLocaleEditorPrefKey = $"EZUtils.Localization.SelectedLocale.{localeSynchronizationKey}";
            SelectedLocale = initialLocale;
            UI = new LocaleSelectionUI(this);
        }

        public static CatalogLocaleSynchronizer Get(string localeSynchronizationKey, Locale initialLocale)
            => synchronizers.TryGetValue(localeSynchronizationKey, out CatalogLocaleSynchronizer value)
                ? value
                : synchronizers[localeSynchronizationKey] = new CatalogLocaleSynchronizer(localeSynchronizationKey, initialLocale);

        public void Register(CatalogReference catalogReference)
        {
            subscribers.Add(catalogReference);

            //we take first registration as a signal that it's safe to use editorprefs, which can't be used in cctor land
            //EZLocalization only calls this when it's safe
            if (subscribers.Count == 1)
            {
                string prefValue = EditorPrefs.GetString(selectedLocaleEditorPrefKey);
                CultureInfo storedLocale = !string.IsNullOrEmpty(prefValue)
                    ? CultureInfo.GetCultureInfo(prefValue)
                    : null;

                _ = SelectLocaleOrNative(UnityLanguageTracking.NewlySelectedUnityEditorLanguage, storedLocale);
            }
        }

        public void SelectLocale(Locale locale)
        {
            SelectedLocale = locale;
            foreach (CatalogReference reference in subscribers)
            {
                _ = reference.Catalog.TrySelectLocale(locale);
            }
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            UI.ReportChange();
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
            UI.ReportChange();
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
            UI.ReportChange();
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
            UI.ReportChange();
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
            UI.ReportChange();
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
            UI.ReportChange();
            return SelectedLocale;
        }
        //needed to disambiguate between locale vs cultureinfo params arrays
        public Locale SelectLocaleOrNative() => SelectLocaleOrNative(Array.Empty<Locale>());
    }
}
