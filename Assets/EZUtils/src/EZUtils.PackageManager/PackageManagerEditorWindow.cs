namespace EZUtils.PackageManager
{
    using System.Collections.Generic;
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
                "Assets/EZUtils/PackageManager/src/EZUtils.PackageManager/PackageManagerEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            // ListView listView = rootVisualElement.Q<ListView>();
            // List<string> items = new List<string>() { "florp" };
            // listView.selectionType = SelectionType.None;
            // listView.makeItem = () =>
            // {
            //     return new Button();
            // };
            // listView.bindItem = (e, i) =>
            // {
            //     Button b = (Button)e;
            //     b.text = $"florp_{i}";
            //     b.clicked += () =>
            //     {
            //         items.Add(b.text);
            //         listView.Refresh();
            //     };
            // };
            // listView.itemsSource = items;
            // listView.Refresh();
        }
    }
}
