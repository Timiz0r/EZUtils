namespace EZUtils.Localization
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Localization;
    using UnityEngine.UIElements;

    public class ManualTestingEditorWindow : EditorWindow
    {
        private EZLocalization localization;

        [MenuItem("EZUtils/Localization Manual Testing", isValidateFunction: false, priority: 0)]
        public static void ManualTesting()
        {
            ManualTestingEditorWindow window = GetWindow<ManualTestingEditorWindow>("EZLocalization manual testing");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.localization/ManualTestingEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            localization = EZLocalization.Create(
                "Assets/EZUtils/EZLocalization/ManualTesting",
                new LocaleIdentifier("en-US"),
                new LocaleIdentifier("ja-JP"));
            LocalizationContext localizationContext = localization.GetContext("manualtesting1", "key.lol");

            foreach (VisualElement element in rootVisualElement.Query().Descendents<VisualElement>().ToList())
            {

                if (element is Button button && button.text.StartsWith("loc:", StringComparison.Ordinal))
                {
                    button.text = Localize(button.text);
                }
                else if (element is Label label && label.text.StartsWith("loc:", StringComparison.Ordinal))
                {
                    label.text = Localize(label.text);
                }
            }


            string Localize(string text)
            {
                if (text.StartsWith("loc:", StringComparison.Ordinal))
                {
                    text = localizationContext.GetString(text.Substring(4));
                }
                return text;
            }
        }
    }
}
