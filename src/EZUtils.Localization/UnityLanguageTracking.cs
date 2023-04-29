namespace EZUtils.Localization
{
    using System;
    using System.Globalization;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    public static class UnityLanguageTracking
    {
        public static CultureInfo NewlySelectedUnityEditorLanguage { get; private set; }

        //when the language is changed, we get a domain reload
        //so tracking language changes requires persisting it somewhere and looking it up
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
                NewlySelectedUnityEditorLanguage = CultureInfo.GetCultureInfo(
                    (string)getCultureMethod.Invoke(null, new object[] { currentEditorLanguage }));
            }
            else NewlySelectedUnityEditorLanguage = null;
        }
    }
}
