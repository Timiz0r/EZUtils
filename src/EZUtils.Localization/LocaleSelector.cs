namespace EZUtils.Localization
{
    using System.Collections.Generic;
    using System.Globalization;
    using UnityEditor;


    //first, we integrate with unity editor language
    //if the unity editor is changed to a language supported, then we use that locale
    //  otherwise, we keep using current locale
    //next, we can set one unity editor-wide (separate from editor language).
    //  this is driven by a user setting it, as well as based on unity editor language selection
    //  the tricky thing is not all catalogs will support the editor-wide language.
    //  we fallback to editor in that case
    public class LocaleSelector
    {
        //keep them in sync in case selected across multiple instantiations
        private readonly string editorPrefKey;
        private readonly CatalogReference catalog;
        private static readonly Dictionary<string, LocaleSelector> localeSelectors = new Dictionary<string, LocaleSelector>();

        //since these are often created in cctors, we can't use editorprefs that early
        [InitializeOnLoadMethod]
        private static void LoadInitialLocale()
        {
            foreach (LocaleSelector localeSelector in localeSelectors.Values)
            {
                string prefValue = EditorPrefs.GetString(localeSelector.editorPrefKey);
                if (!string.IsNullOrEmpty(prefValue))
                {
                    CultureInfo locale = CultureInfo.GetCultureInfo(prefValue);
                    localeSelector.SelectLocale(locale);
                }
                else
                {
                    //TODO: this will change in some way when we get syncing done
                    localeSelector.SelectLocale(CultureInfo.GetCultureInfo("en"));
                }
            }
        }

        private LocaleSelector(string domain, CatalogReference catalog)
        {
            this.catalog = catalog;

            editorPrefKey = $"EZUtils.Localization.SelectedLocale.{domain}";
        }

        public CultureInfo SelectedLocale { get; private set; }

        public void SelectLocale(CultureInfo locale)
        {
            if (!catalog.Catalog.Supports(locale)) return;

            EditorPrefs.SetString(editorPrefKey, locale.Name);
            SelectedLocale = locale;
            //TODO: the most obvious problem here is that an updated catalog will have its language unset
            //but want to investigate the possiblity of combining catalogreference and this class
            //prob wont on size reasons alone, but maybe
            catalog.SelectLocale(locale);
        }

        //TODO: need to write an editor language change detection mechanism
        //we might need to detect in an update loop, but first look at the underlying code to see if we can
        //hijack some of the things it calls

        public static LocaleSelector Create(string domain, CatalogReference catalog)
        {
            if (localeSelectors.TryGetValue(domain, out LocaleSelector localeSelector))
            {
                return localeSelector;
            }

            LocaleSelector result = localeSelectors[domain] = new LocaleSelector(domain, catalog);
            return result;
        }
    }
}
