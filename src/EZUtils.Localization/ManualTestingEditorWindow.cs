namespace EZUtils.Localization
{
    using System;
    using System.Globalization;
    using UnityEditor;
    using UnityEngine.UIElements;

    public class ManualTestingEditorWindow : EditorWindow
    {
        private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder("Packages/com.timiz0r.ezutils.editorenhancements");

        [MenuItem("EZUtils/Localization Manual Testing", isValidateFunction: false, priority: 0)]
        public static void ManualTesting()
        {
            ManualTestingEditorWindow window = GetWindow<ManualTestingEditorWindow>();
            window.Show();
            Retranslate();
        }

        private static void Retranslate()
        {
            //the only known way to cause CreateGUI to get reloaded again
            //well, without much more invasive means
            //TODO: theoretically, a Clear followed by a manual invocation of CreateGUI should work perfectly fine, as long
            //as a creategui hasn't done something insane like modifying higher-up elements. if we can get the design just right, maybe do it?
            //so, what we started doing is adding a bunch of tracking code for objects/windows and elements.
            //then we got this single call to rule them all, at the cost of it being slow as hell (not that we're changing langs  that often).
            //with a clear and manual invocation...
            //  still need to track windows (heck, even might even with the one call to rule them all?)
            //
            //edit: actually doesnt work unless the settings window is open
            //_ = typeof(EditorGUIUtility)
            //    .GetMethod("NotifyLanguageChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            //    .Invoke(null, new object[]
            //    {
            //        Type
            //            .GetType("UnityEditor.LocalizationDatabase, UnityEditor")
            //            .GetProperty("currentEditorLanguage", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            //            .GetValue(null)
            //    });
            //EditorUtility.RequestScriptReload();
            if (new System.Random().Next(0, 2) == 0)
            {
                loc.Test(CultureInfo.GetCultureInfo("en"));
            }
            else
            {
                loc.Test(CultureInfo.GetCultureInfo("ja"));
            }
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.localization/ManualTestingEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);
            loc.TranslateElementTree(rootVisualElement);
            loc.TranslateWindow(this, titleText: "EZLocalization manual testing");


            // string Localize(string text)
            // {
            //     if (text.StartsWith("loc:", StringComparison.Ordinal))
            //     {
            //         text = localizationContext.GetString(text.Substring(4));
            //     }
            //     return text;
            // }
        }
    }
}
