namespace EZUtils.EditorEnhancements
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEngine.UIElements;

    //will generally prefer basic english for this portion, since it's the most viable single language
    //TODO: the most ideal thing to do, at least at time of writing, is to translate each item
    //in the language pack list to its own language
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
                e.Q<Label>(name: "status").text = languagePack.IsInstalled ? "Installed" : "Not installed";
                e.SetEnabled(!languagePack.IsInstalled);
                e.Q<Button>().clicked += async () => await languagePack.Install();
            };

            _ = Load();
            async Task Load()
            {
                IReadOnlyList<UnityEditorLanguagePack> languagePacks = await UnityEditorLanguagePack.GetAvailable();
                listView.itemsSource = languagePacks.ToArray();
                listView.Rebuild();
            }
        }
    }
}
