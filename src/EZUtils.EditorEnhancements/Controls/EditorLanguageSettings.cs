namespace EZUtils.EditorEnhancements
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEngine.UIElements;

    public class EditorLanguageSettings : VisualElement
    {

        public EditorLanguageSettings()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.editorenhancements/Controls/EditorLanguageSettings.uxml");
            visualTree.CommonUIClone(this);

            ListView listView = this.Q<ListView>();
            listView.selectionType = SelectionType.None;
            listView.makeItem = () => new DownloadableLanguagePack();
            listView.bindItem = (e, i) =>
            {
                UnityEditorLanguagePack languagePack = ((IReadOnlyList<UnityEditorLanguagePack>)listView.itemsSource)[i];
                e.Q<Label>(name: "name").text = languagePack.Name;
                e.Q<Label>(name: "status").text = languagePack.IsInstalled ? "インストール済み" : "インストールされてない";
                e.SetEnabled(!languagePack.IsInstalled);
            };

            _ = Load();
            async Task Load()
            {
                IReadOnlyList<UnityEditorLanguagePack> languagePacks = await UnityEditorLanguagePack.GetAvailable();
                listView.itemsSource = languagePacks.ToArray();
                listView.Refresh();
            }
        }
    }
}
