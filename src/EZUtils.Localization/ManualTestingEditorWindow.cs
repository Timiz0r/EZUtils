namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class ManualTestingEditorWindow : EditorWindow
    {
        [MenuItem("EZUtils/Localization Manual Testing", isValidateFunction: false, priority: 0)]
        public static void ManualTesting()
        {
            ManualTestingEditorWindow window = GetWindow<ManualTestingEditorWindow>("EZUtils Package Manager");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.localization/ManualTestingEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);
                foreach (VisualElement element in rootVisualElement.Query().Descendents<VisualElement>().ToList())
                {

                    Debug.LogWarning("wat2");
                    if (element is Button button && button.text.StartsWith("loc:", StringComparison.Ordinal))
                    {
                        Debug.LogWarning("wat3");
                        button.text = "lolol" + button.text;
                    }
                    else if (element is Label label && label.text.StartsWith("loc:", StringComparison.Ordinal))
                    {
                        Debug.LogWarning("wat4");
                        label.text = "lolol2" + label.text;
                    }
                }

        }
    }
}
