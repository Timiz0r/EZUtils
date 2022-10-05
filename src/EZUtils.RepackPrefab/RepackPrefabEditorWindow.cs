namespace EZUtils.RepackPrefab
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class RepackPrefabEditorWindow : EditorWindow
    {
        [MenuItem("EZUtils/Repack prefab", isValidateFunction: false, priority: 0)]
        public static void PackageManager()
        {
            RepackPrefabEditorWindow window = GetWindow<RepackPrefabEditorWindow>("Repack prefab");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.repackprefab/RepackPrefabEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            ObjectField referenceObject = rootVisualElement.Q<ObjectField>(name: "referenceObject");
            referenceObject.objectType = typeof(GameObject);

            ObjectField referencePrefab = rootVisualElement.Q<ObjectField>(name: "referencePrefab");
            referencePrefab.objectType = typeof(GameObject);

            //TODO: don't allow the button to be clicked unless objectfields valid
            rootVisualElement.Q<Button>(name: "repackPrefab").clicked += ()
                => RepackPrefab.Repack((GameObject)referenceObject.value, (GameObject)referencePrefab.value);
        }
    }
}
