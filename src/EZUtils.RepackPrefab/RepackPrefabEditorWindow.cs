namespace EZUtils.RepackPrefab
{
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class RepackPrefabEditorWindow : EditorWindow
    {
        private readonly UIValidator validator = new UIValidator();

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

            TypedObjectField<GameObject> sourceObject =
                rootVisualElement.Q<ObjectField>(name: "sourceObject").Typed<GameObject>();
            TypedObjectField<GameObject> basePrefab =
                rootVisualElement.Q<ObjectField>(name: "basePrefab").Typed<GameObject>();

            Button repackPrefab = rootVisualElement.Q<Button>(name: "repackPrefab");
            repackPrefab.clicked += ()
                => RepackPrefab.Repack(sourceObject.value, basePrefab.value);

            validator.AddValueValidation(sourceObject, passCondition: o => o != null);
            validator.AddValueValidation(basePrefab, passCondition: o => o != null);
            validator.DisableIfInvalid(repackPrefab);
        }
    }
}
