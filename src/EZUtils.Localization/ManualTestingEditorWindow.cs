namespace EZUtils.Localization
{
    using System;
    using System.Globalization;
    using UnityEditor;
    using UnityEngine.UIElements;

    public class ManualTestingEditorWindow : EditorWindow
    {
        private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder("Packages/com.timiz0r.ezutils.editorenhancements");
        private static int temp;

        [MenuItem("EZUtils/Localization Manual Testing", isValidateFunction: false, priority: 0)]
        public static void ManualTesting()
        {
            ManualTestingEditorWindow window = GetWindow<ManualTestingEditorWindow>();
            window.Show();
            Retranslate();
        }

        private static void Retranslate()
        {
            temp = (temp + 1) % 2;
            if (temp == 0)
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
