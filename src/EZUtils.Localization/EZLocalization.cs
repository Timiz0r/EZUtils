namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    //ports-and-adapters-wise, EZLocalization is a driver adapter connecting unity editor to a catalog
    public class EZLocalization
    {
        private static CultureInfo newlySelectedUnityEditorLanguage;
        private static readonly DelayInitializer delayInitializer = new DelayInitializer();
        private readonly string selectedLocaleEditorPrefKey;
        private readonly CatalogReference catalog;

        [InitializeOnLoadMethod]
        private static void UnityInitialize()
        {
            TrackUnityEditorLanguage(out newlySelectedUnityEditorLanguage);
            delayInitializer.Initialize();
        }

        private EZLocalization(string domain, CatalogReference catalog)
        {
            this.catalog = catalog;
            selectedLocaleEditorPrefKey = $"EZUtils.Localization.SelectedLocale.{domain}";

            //this goes in ctor instead of factory methods because we presumably want this logic for any (future)
            //construction of EZLocalization
            delayInitializer.Execute(() =>
            {
                //EZLocalization instances are usually created via cctor,
                //but certain things like AssetDatabase and EditorPrefs cannot be used there
                string prefValue = EditorPrefs.GetString(selectedLocaleEditorPrefKey);
                if (!string.IsNullOrEmpty(prefValue))
                {
                    CultureInfo locale = CultureInfo.GetCultureInfo(prefValue);
                    _ = SelectLocaleOrNative(newlySelectedUnityEditorLanguage, locale);
                }
                else
                {
                    SelectLocale(newlySelectedUnityEditorLanguage);
                }
            });
        }

        public static EZLocalization ForCatalogUnder(string root, Locale nativeLocale)
        {
            root = root.Trim(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

            CatalogReference catalog = CatalogDatabase.GetCatalogReference(root, nativeLocale);
            EZLocalization result = new EZLocalization(root, catalog);

            return result;
        }
        public static EZLocalization ForCatalogUnder(string root) => ForCatalogUnder(root, Locale.English);

        //TODO: dont think we still have quite the right behavior, where we want the pref to be editor wide?
        public void SelectLocale(Locale locale)
        {
            catalog.SelectLocale(locale);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, locale.CultureInfo.Name);
        }
        public void SelectLocale(CultureInfo cultureInfo)
        {

            Locale correspondingLocale = catalog.SelectLocale(cultureInfo);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, correspondingLocale.CultureInfo.Name);
        }
        public Locale SelectLocaleOrNative(params Locale[] locales)
        {
            Locale selectedLocale = catalog.SelectLocaleOrNative(locales);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, selectedLocale.CultureInfo.Name);
            return selectedLocale;
        }
        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
        {
            Locale selectedLocale = catalog.SelectLocaleOrNative(cultureInfos);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, selectedLocale.CultureInfo.Name);
            return selectedLocale;
        }

        //while we support custom retranslation via IRetranslatable, the most recommended way to support retranslation
        //for elements created in-code is to call EZLocalization.TranslateElementTree for the newly created element.
        //but for non-TextElements and non-BaseField`1s, we have IRetranslatable.
        //TODO: plural implementation? kinda hard to do since this is recursive.
        //perhaps we need to exclude plural ones in the recursive one, and have the plural overload not be recursive
        //still gotta decide how to get the value inside. easiest is {0}
        public void TranslateElementTree(VisualElement rootElement)
            //descendents includes element, as well
            => rootElement.Query().Descendents<VisualElement>().ForEach(element =>
            {
                if (element is IRetranslatable retranslatable)
                {
                    //making this first so that we can override default behavior if necessary
                    retranslatable.Retranslate();
                }
                else if (element is TextElement textElement
                    && textElement.text is string teOriginalValue
                    && teOriginalValue.StartsWith("loc:", StringComparison.Ordinal))
                {
                    teOriginalValue = teOriginalValue.Substring(4);
                    catalog.TrackRetranslatable(textElement, () => textElement.text = T(teOriginalValue));
                }
                else if (element.GetType() is Type elementType
                    && InheritsFromGenericType(elementType.GetType(), typeof(BaseField<>))
                    && elementType.GetProperty("label", BindingFlags.Public | BindingFlags.Instance) is PropertyInfo labelProperty
                    && labelProperty.GetValue(element) is string lbOriginalValue
                    && lbOriginalValue.StartsWith("loc:", StringComparison.Ordinal))
                {
                    lbOriginalValue = lbOriginalValue.Substring(4);
                    catalog.TrackRetranslatable(element, () => labelProperty.SetValue(element, T(lbOriginalValue)));
                }
            });
        public string T(string id) => catalog.Catalog.T(id);
        public void TranslateWindowTitle(EditorWindow window, string titleText)
            => catalog.TrackRetranslatable(window, () => window.titleContent.text = T(titleText));

        //when the language is changed, we get a domain reload
        //so tracking language changes requires persisting it somewhere and looking it up, all from a cctor
        private static void TrackUnityEditorLanguage(out CultureInfo newlySelectedUnityEditorLanguage)
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

        private static bool InheritsFromGenericType(Type typeToInspect, Type genericTypeDefinition)
        {
            if (typeToInspect == null) return false;
            if (typeToInspect.IsGenericType
                && typeToInspect.GetGenericTypeDefinition() is Type genericType
                && genericType == genericTypeDefinition) return true;
            return InheritsFromGenericType(typeToInspect.BaseType, genericTypeDefinition);
        }
    }
}
