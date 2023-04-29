namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

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
    //
    //in the event CatalogReference needs to reload a new GetTextCatalog, it reads CatalogLocaleSynchronizer.SelectedLocale
    //to do it.
    internal class CatalogLocaleSynchronizer
    {
        private static readonly Dictionary<string, CatalogLocaleSynchronizer> synchronizers =
            new Dictionary<string, CatalogLocaleSynchronizer>();
        private static CultureInfo newlySelectedUnityEditorLanguage;

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
                _ = value.SelectLocaleOrNative(newlySelectedUnityEditorLanguage, storedLocale);
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
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, locale.CultureInfo.Name);
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
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, cultureInfo.Name);
            return SelectedLocale;
        }

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

        //when the language is changed, we get a domain reload
        //so tracking language changes requires persisting it somewhere and looking it up, all from a cctor
        [InitializeOnLoadMethod]
#pragma warning disable IDE0051 //Private member is unused; unity message
        private static void UnityInitialize()
#pragma warning restore IDE0051
        {
            Type localizationDatabaseType = Type.GetType("UnityEditor.LocalizationDatabase, UnityEditor");

            const string editorLanguageKey = "EZUtils.Localization.TrackedUnityEditorLanguage";
            string previouslySetEditorLanguage = EditorPrefs.GetString(editorLanguageKey);

            PropertyInfo editorlanguageProperty = localizationDatabaseType.GetProperty(
                "currentEditorLanguage", BindingFlags.Public | BindingFlags.Static);
            SystemLanguage currentEditorLanguage = (SystemLanguage)editorlanguageProperty.GetValue(null);

            if (previouslySetEditorLanguage != currentEditorLanguage.ToString())
            {
                EditorPrefs.SetString(editorLanguageKey, currentEditorLanguage.ToString());
                MethodInfo getCultureMethod = localizationDatabaseType.GetMethod(
                    "GetCulture", BindingFlags.Public | BindingFlags.Static);
                newlySelectedUnityEditorLanguage = CultureInfo.GetCultureInfo(
                    (string)getCultureMethod.Invoke(null, new object[] { currentEditorLanguage }));
            }
            else newlySelectedUnityEditorLanguage = null;
        }
    }
}
