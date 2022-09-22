namespace EZUtils.PackageManager
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine.UIElements;

    public class PackageManagerEditorWindow : EditorWindow
    {
        //not working for some reason
        public VisualTreeAsset uxml;

        [MenuItem("EZUtils/Package Manager", isValidateFunction: false, priority: 0)]
        public static void PackageManager()
        {
            PackageManagerEditorWindow window = GetWindow<PackageManagerEditorWindow>("EZUtils Package Manager");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.packagemanager/PackageManagerEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            ListView listView = rootVisualElement.Q<ListView>();
            listView.selectionType = SelectionType.None;
            listView.makeItem = () =>
            {
                return new Button();
            };
            listView.bindItem = (e, i) =>
            {
                Button b = (Button)e;
                PackageInformation targetPackage = ((IReadOnlyList<PackageInformation>)listView.itemsSource)[i];
                b.text = targetPackage.Name;
            };
            listView.Refresh();

            PackageRepository packageRepo = new PackageRepository();
            rootVisualElement.Q<Button>(name: "refreshPackages").clicked += async () =>
            {
                IReadOnlyList<PackageInformation> packages = await packageRepo.ListAsync();

                listView.itemsSource = packages.ToArray();
                // listView.Refresh();
            };
        }
    }
}
