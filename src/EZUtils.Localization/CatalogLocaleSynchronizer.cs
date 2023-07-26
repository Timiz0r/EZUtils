namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using EZUtils.Localization.UIElements;
    using UnityEditor;

    //TODO: there is a bug that results from inconsistent locale sets across catalogs
    //let's say one catalog has korean, and others don't. we set the editorpref to korean.
    //then, let's say the catalog for `subscribers.Count == 1` doesn't have korean.
    //then, the editorpref gets set effectively back to native.
    //this happens because we try to get a locale out of a set of cultureinfos, fail, and set back the native locale.
    //it's hard to fix in the current design, and it's being deferred because it's preferable to only ever have consistent catalogs.
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
            //note that the above is effectively a superset of the below
            else
            {
                catalogReference.RecordIntendedLocale(SelectedLocale);
                if (catalogReference.Catalog.TrySelectLocale(SelectedLocale))
                {
                    catalogReference.RecordCurrentLocale(SelectedLocale);
                }
            }
        }

        private void FinishLocaleSelection()
        {
            foreach (CatalogReference reference in subscribers)
            {
                reference.RecordIntendedLocale(SelectedLocale);
            }

            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            UI.RegenerateMenu();
        }

        public void SelectLocale(Locale locale)
        {
            SelectedLocale = locale;
            foreach (CatalogReference reference in subscribers)
            {
                if (reference.Catalog.TrySelectLocale(locale))
                {
                    reference.RecordCurrentLocale(locale);
                }
            }
            FinishLocaleSelection();
        }
        public Locale SelectLocale(CultureInfo cultureInfo)
        {
            bool foundLocale = false;
            foreach (CatalogReference reference in subscribers)
            {
                if (reference.Catalog.TrySelectLocale(cultureInfo, out Locale correspondingLocale))
                {
                    if (!foundLocale)
                    {
                        foundLocale = true;
                        SelectedLocale = correspondingLocale;
                    }
                    reference.RecordCurrentLocale(correspondingLocale);
                }
            }
            FinishLocaleSelection();
            return SelectedLocale;
        }

        public bool TrySelectLocale(Locale locale)
        {
            bool foundLocale = false;
            foreach (CatalogReference reference in subscribers)
            {
                if (reference.Catalog.TrySelectLocale(locale))
                {
                    if (!foundLocale)
                    {
                        foundLocale = true;
                        SelectedLocale = locale;
                    }
                    reference.RecordCurrentLocale(locale);
                }
            }
            FinishLocaleSelection();
            return foundLocale;
        }
        public bool TrySelectLocale(CultureInfo cultureInfo, out Locale correspondingLocale)
        {
            bool foundLocale = false;
            foreach (CatalogReference reference in subscribers)
            {
                if (reference.Catalog.TrySelectLocale(cultureInfo, out Locale currentReferenceLocale))
                {
                    if (!foundLocale)
                    {
                        foundLocale = true;
                        SelectedLocale = currentReferenceLocale;
                    }
                    reference.RecordCurrentLocale(currentReferenceLocale);
                }
            }
            FinishLocaleSelection();
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
                    && Array.IndexOf(locales, selectedLocale) > -1) //aka is not native
                {
                    if (!foundLocale)
                    {
                        foundLocale = true;
                        SelectedLocale = selectedLocale;
                    }
                    reference.RecordCurrentLocale(selectedLocale);
                }
            }
            FinishLocaleSelection();
            return SelectedLocale;
        }
        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
        {
            bool foundLocale = false;
            foreach (CatalogReference reference in subscribers)
            {
                if (reference.Catalog.SelectLocaleOrNative(cultureInfos) is Locale selectedLocale
                    && Array.Exists(cultureInfos, c => c == selectedLocale.CultureInfo)) //aka is not native
                {
                    if (!foundLocale)
                    {
                        foundLocale = true;
                        SelectedLocale = selectedLocale;
                    }
                    reference.RecordCurrentLocale(selectedLocale);
                }
            }
            FinishLocaleSelection();
            return SelectedLocale;
        }
        //needed to disambiguate between locale vs cultureinfo params arrays
        public Locale SelectLocaleOrNative() => SelectLocaleOrNative(Array.Empty<Locale>());
    }
}
