namespace EZUtils.Localization
{
    using System.Globalization;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;
    using static Proxy.Localization;

    public class ManualTestingEditorWindow : EditorWindow
    {
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
                _ = SelectLocale(CultureInfo.GetCultureInfo("en"));
            }
            else
            {
                _ = SelectLocale(CultureInfo.GetCultureInfo("ja"));
            }
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.localization/ManualTestingEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);
            TranslateElementTree(rootVisualElement);
            TranslateWindowTitle(this, titleText: "EZLocalization manual testing");

            _ = Proxy.Localization.T("wat");
            Debug.Log(T(LocalizationParameter.Count));
            Debug.Log(T("the rain in spain falls mainly on the plain"));
            decimal value = new System.Random().Next();
            Debug.Log(T($"There is {value} cupcake.", value, $"There are {value} cupcakes."));

            rootVisualElement.Q<Button>().clicked += () =>
            {
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
