namespace EZUtils
{
    using System;
    using UnityEditor;
    using UnityEngine.UIElements;

    public static class CommonUI
    {
        private static readonly Lazy<StyleSheet> commonStyles = new Lazy<StyleSheet>(
            () => AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.timiz0r.ezutils.common/UI/CommonStyles.uss"));
        public static VisualElement CommonUIClone(this VisualTreeAsset visualTreeAsset)
        {
            VisualElement result = visualTreeAsset.CloneTree();
            result.styleSheets.Add(commonStyles.Value);
            return result;
        }
        public static void CommonUIClone(this VisualTreeAsset visualTreeAsset, VisualElement target)
        {
            visualTreeAsset.CloneTree(target);
            StyleSheet commonStyles = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.timiz0r.ezutils.common/UI/CommonStyles.uss");
            target.styleSheets.Add(commonStyles);
        }
    }
}
