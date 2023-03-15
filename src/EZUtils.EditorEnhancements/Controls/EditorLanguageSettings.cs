namespace EZUtils.EditorEnhancements
{
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    public class EditorLanguageSettings : VisualElement
    {

        public EditorLanguageSettings()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.editorenhancements/Controls/EditorLanguageSettings.uxml");
            visualTree.CommonUIClone(this);

        }
    }
}
