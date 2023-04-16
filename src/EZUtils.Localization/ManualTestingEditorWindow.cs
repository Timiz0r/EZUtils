namespace EZUtils.Localization
{
    using System.Globalization;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class ManualTestingEditorWindow : EditorWindow
    {
        [GenerateLanguage("ja", "ja.po", Other = "")]
        [GenerateLanguage("ko", "ko.po", Other = "")]
        private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder("Packages/com.timiz0r.ezutils.localization");
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
                loc.SelectLocale(CultureInfo.GetCultureInfo("en"));
            }
            else
            {
                loc.SelectLocale(CultureInfo.GetCultureInfo("ja"));
            }
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.localization/ManualTestingEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);
            loc.TranslateElementTree(rootVisualElement);
            loc.TranslateWindowTitle(this, titleText: "EZLocalization manual testing");

            _ = Proxy.Localization.T("wat");
            Debug.Log(loc.T(LocalizationParameter.Count));
            Debug.Log(loc.T("the rain in spain falls mainly on the plain"));
            decimal value = new System.Random().Next();
            Debug.Log(loc.T($"There is {value} cupcake.", value, $"There are {value} cupcakes."));

            rootVisualElement.Q<Button>().clicked += () =>
            {
                EZLocalizationExtractor extractor = new EZLocalizationExtractor();
                extractor.ExtractFrom();
            };


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
