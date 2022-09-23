namespace EZUtils.PackageManager
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EZUtils.PackageManager.UIElements;
    using UnityEditor;
    using UnityEngine.UIElements;

    public class PackageManagerEditorWindow : EditorWindow
    {
        //not working for some reason
        public VisualTreeAsset uxml;
        private readonly PackageRepository packageRepository = new PackageRepository();

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

            async Task Refresh()
            {
                IReadOnlyList<PackageInformation> packages = await packageRepository.ListAsync();

                listView.itemsSource = packages.ToArray();
                listView.Refresh();
            }

            listView.makeItem = () => new PackageInformationItem(packageRepository, async () => await Refresh());
            listView.bindItem = (e, i) =>
            {
                PackageInformationItem item = (PackageInformationItem)e;
                PackageInformation targetPackage = ((IReadOnlyList<PackageInformation>)listView.itemsSource)[i];
                item.Rebind(targetPackage);
            };
            rootVisualElement.Q<Button>(name: "refreshPackages").clicked += async () => await Refresh();

            packageRepository.CheckForScopedRegistry();

            _ = Refresh();
        }
    }
}
