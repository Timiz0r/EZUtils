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
    public class CatalogLocaleSynchronizer
    {
        private static CultureInfo newlySelectedUnityEditorLanguage;
        private static readonly Dictionary<string, CatalogLocaleSynchronizer> synchronizers =
            new Dictionary<string, CatalogLocaleSynchronizer>();

        private readonly string selectedLocaleEditorPrefKey;
        private readonly CatalogReference catalogReference;

        public Locale SelectedLocale { get; private set; }

        private CatalogLocaleSynchronizer(string localeSynchronizationKey, CatalogReference catalogReference)
        {
            selectedLocaleEditorPrefKey = $"EZUtils.Localization.SelectedLocale.{localeSynchronizationKey}";
            this.catalogReference = catalogReference;

            string prefValue = EditorPrefs.GetString(selectedLocaleEditorPrefKey);
            if (!string.IsNullOrEmpty(prefValue))
            {
                CultureInfo locale = CultureInfo.GetCultureInfo(prefValue);
                //if newlySelectedUnityEditorLanguage is null then we'll prefer one from setting
                _ = SelectLocaleOrNative(newlySelectedUnityEditorLanguage, locale);
            }
            else if (newlySelectedUnityEditorLanguage != null)
            {
                _ = SelectLocaleOrNative(newlySelectedUnityEditorLanguage);
            }
            else
            {
                _ = SelectLocaleOrNative(Array.Empty<Locale>());
            }
        }

        public static CatalogLocaleSynchronizer Register(string localeSynchronizationKey, CatalogReference catalogReference)
        {
            if (synchronizers.TryGetValue(localeSynchronizationKey, out CatalogLocaleSynchronizer value))
            {
                return value;
            }

            return synchronizers[localeSynchronizationKey] = new CatalogLocaleSynchronizer(
                localeSynchronizationKey, catalogReference);
        }

        public void SelectLocale(Locale locale)
        {
            SelectedLocale = locale;
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, locale.CultureInfo.Name);
            catalogReference.Catalog.SelectLocale(locale);
        }

        public Locale SelectLocale(CultureInfo cultureInfo)
        {
            SelectedLocale = catalogReference.Catalog.SelectLocale(cultureInfo);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, cultureInfo.Name);
            return SelectedLocale;
        }

        public Locale SelectLocaleOrNative(params Locale[] locales)
        {
            SelectedLocale = catalogReference.Catalog.SelectLocaleOrNative(locales);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            return SelectedLocale;
        }

        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
        {
            SelectedLocale = catalogReference.Catalog.SelectLocaleOrNative(cultureInfos);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, SelectedLocale.CultureInfo.Name);
            return SelectedLocale;
        }

        //when the language is changed, we get a domain reload
        //so tracking language changes requires persisting it somewhere and looking it up, all from a cctor
        [InitializeOnLoadMethod]
        private static void UnityInitialize()
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
