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
        private readonly CatalogReference representativeCatalogReference;
        private readonly List<CatalogReference> subscribers = new List<CatalogReference>();

        public Locale SelectedLocale { get; private set; }

        private CatalogLocaleSynchronizer(string localeSynchronizationKey, CatalogReference representativeCatalogReference)
        {
            selectedLocaleEditorPrefKey = $"EZUtils.Localization.SelectedLocale.{localeSynchronizationKey}";
            this.representativeCatalogReference = representativeCatalogReference;
        }

        public static CatalogLocaleSynchronizer Register(string localeSynchronizationKey, CatalogReference catalogReference)
        {
            if (!synchronizers.TryGetValue(localeSynchronizationKey, out CatalogLocaleSynchronizer value))
            {
                synchronizers[localeSynchronizationKey] = value = new CatalogLocaleSynchronizer(
                    localeSynchronizationKey, catalogReference);

                string prefValue = EditorPrefs.GetString(value.selectedLocaleEditorPrefKey);
                CultureInfo storedLocale = !string.IsNullOrEmpty(prefValue)
                    ? CultureInfo.GetCultureInfo(prefValue)
                    : null;
                _ = value.SelectLocaleOrNative(newlySelectedUnityEditorLanguage, storedLocale);
            }
            else
            {
                //we use representativeCatalogReference to be the one that gets SetLocale calls in order to get the
                //resulting selected locale. it's effectively the first EZLocalization instance to
                //get a CatalogLocaleSynchronizer.
                //the rest of the subscribers are then synchronized using the same call, so we want to avoid calling
                //SetLocale twice for that first EZLocalization instance.
                value.subscribers.Add(catalogReference);
            }
            return value;
        }

        public void SelectLocale(Locale locale)
        {
            SelectedLocale = locale;
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, locale.CultureInfo.Name);
            representativeCatalogReference.Catalog.SelectLocale(locale);
            foreach (CatalogReference reference in subscribers)
            {
                reference.Catalog.SelectLocale(locale);
            }
        }

        public Locale SelectLocale(CultureInfo cultureInfo)
        {
            SelectedLocale = representativeCatalogReference.Catalog.SelectLocale(cultureInfo);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, cultureInfo.Name);
            foreach (CatalogReference reference in subscribers)
            {
                reference.Catalog.SelectLocale(SelectedLocale);
            }
            return SelectedLocale;
        }

        public Locale SelectLocaleOrNative(params Locale[] locales)
        {
            SelectedLocale = representativeCatalogReference.Catalog.SelectLocaleOrNative(locales);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            foreach (CatalogReference reference in subscribers)
            {
                reference.Catalog.SelectLocale(SelectedLocale);
            }
            return SelectedLocale;
        }

        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
        {
            SelectedLocale = representativeCatalogReference.Catalog.SelectLocaleOrNative(cultureInfos);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            foreach (CatalogReference reference in subscribers)
            {
                reference.Catalog.SelectLocale(SelectedLocale);
            }
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
