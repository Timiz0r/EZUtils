namespace EZUtils.EditorEnhancements
{
    using UnityEditor;
    using UnityEngine.UIElements;

    public class DownloadableLanguagePack : VisualElement
    {

        public DownloadableLanguagePack()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.editorenhancements/Controls/DownloadableLanguagePack.uxml");
            visualTree.CloneTree(this);
        }
    }
}
