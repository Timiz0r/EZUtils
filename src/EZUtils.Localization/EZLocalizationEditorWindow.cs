namespace EZUtils.Localization
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine.UIElements;

    public class EZLocalizationEditorWindow : EditorWindow
    {
        [MenuItem("EZUtils/EZLocalization editing", isValidateFunction: false, priority: 0)]
        public static void ShowWindow()
        {
            EZLocalizationEditorWindow window = GetWindow<EZLocalizationEditorWindow>("EZLocalization");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.localization/EZLocalizationEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);
        }
    }
}
