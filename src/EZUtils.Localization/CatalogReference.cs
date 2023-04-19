namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;


    public class CatalogReference
    {
        private static CultureInfo newlySelectedUnityEditorLanguage;

        private readonly List<(UnityEngine.Object obj, Action action)> retranslatableObjects =
            new List<(UnityEngine.Object obj, Action action)>();
        private readonly List<(VisualElement element, Action action)> retranslatableElements =
            new List<(VisualElement element, Action action)>();
        private readonly string selectedLocaleEditorPrefKey;

        private GetTextCatalog catalog;
        private Locale selectedLocale;
        private bool readInitialLocale;

        //NOTE: this should not be used internally for anything but translation calls
        public GetTextCatalog Catalog
        {
            get
            {
                if (!readInitialLocale)
                {
                    //the below SetLocales will result in a stackoverflow if we dont do this first
                    //it's not like we end up using the catalog for translation, so this is fine
                    readInitialLocale = true;

                    //we do this as late as possible because EZLocalization is often part of cctor of a window, and, when opening
                    //the window for the first time (versus reloading unity), GetString will throw.
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
                        SelectLocale(NativeLocale);
                    }
                }
                return catalog;
            }
        }
        public Locale NativeLocale { get; }

        public CatalogReference(Locale nativeLocale, string localeDomainSetting)
        {
            NativeLocale = selectedLocale = nativeLocale;
            selectedLocaleEditorPrefKey = $"EZUtils.Localization.SelectedLocale.{localeDomainSetting}";
        }

        public void UseUpdatedCatalog(GetTextCatalog catalog)
        {
            this.catalog = catalog;
            //avoid Catalog property, since UseUpdatedCatalog gets called too early for the subsequent
            //EditorPrefs calls to work (and throw)
            this.catalog.SelectLocale(selectedLocale);
            Retranslate();
        }

        public void SelectLocale(Locale locale)
        {
            selectedLocale = locale;
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, locale.CultureInfo.Name);
            Catalog.SelectLocale(locale);
            Retranslate();
        }

        public Locale SelectLocale(CultureInfo cultureInfo)
        {
            selectedLocale = Catalog.SelectLocale(cultureInfo);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, cultureInfo.Name);
            Retranslate();
            return selectedLocale;
        }

        public Locale SelectLocaleOrNative(params Locale[] locales)
        {
            selectedLocale = Catalog.SelectLocaleOrNative(locales);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, selectedLocale.CultureInfo.Name);
            Retranslate();
            return selectedLocale;
        }

        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
        {
            selectedLocale = Catalog.SelectLocaleOrNative(cultureInfos);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, selectedLocale.CultureInfo.Name);
            Retranslate();
            return selectedLocale;
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
