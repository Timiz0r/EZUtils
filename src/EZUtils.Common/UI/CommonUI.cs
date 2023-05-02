namespace EZUtils
{
    using UnityEditor;
    using UnityEngine.UIElements;

    public static class CommonUI
    {
        public static VisualElement CommonUIClone(this VisualTreeAsset visualTreeAsset)
        {
            VisualElement result = visualTreeAsset.CloneTree();
            StyleSheet commonStyles = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.timiz0r.ezutils.common/UI/CommonStyles.uss");
            result.styleSheets.Add(commonStyles);
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
