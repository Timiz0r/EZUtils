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
        public static void ShowWindow()
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

            Toggle preReleaseToggle = rootVisualElement.Q<Toggle>(name: "showPrerelease");

            async Task Refresh()
            {
                rootVisualElement.Q<Label>(name: "status").text = "Refreshing...";

                try
                {
                    bool showPreReleasePackages = preReleaseToggle.value;
                    IReadOnlyList<PackageInformation> packages = await packageRepository.ListAsync(showPreReleasePackages);

                    listView.itemsSource = packages.ToArray();
                    listView.Rebuild();

                    rootVisualElement.Q<Label>(name: "status").text = string.Empty;
                }
                catch when ((rootVisualElement.Q<Label>(name: "status").text = "Refresh failed") == "lol") { throw; }
                finally
                {
                }
            }

            listView.makeItem = () => new PackageInformationItem(packageRepository, async () => await Refresh());
            listView.bindItem = (e, i) =>
            {
                PackageInformationItem item = (PackageInformationItem)e;
                PackageInformation targetPackage = ((IReadOnlyList<PackageInformation>)listView.itemsSource)[i];
                item.Rebind(targetPackage);
            };
            rootVisualElement.Q<Button>(name: "refreshPackages").clicked += async () => await Refresh();

            _ = preReleaseToggle.RegisterValueChangedCallback(
                evt =>
                {
                    EditorPrefs.SetBool("EZUtils.PackageManager.ShowPreReleasePackages", evt.newValue);
                    _ = Refresh();
                });
            preReleaseToggle.SetValueWithoutNotify(
                EditorPrefs.GetBool("EZUtils.PackageManager.ShowPreReleasePackages"));

            packageRepository.CheckForScopedRegistry();

            _ = Refresh();
        }
    }
}
