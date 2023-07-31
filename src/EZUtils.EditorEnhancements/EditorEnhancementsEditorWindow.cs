namespace EZUtils.EditorEnhancements
{
    using System;
    using EZUtils.Localization.UIElements;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    using static Localization;

    public class EditorEnhancementsEditorWindow : EditorWindow
    {

        [InitializeOnLoadMethod]
        private static void UnityInitialize() => AddMenu("EZUtils/Editor Enhancements", priority: 0, ShowWindow);

        [MenuItem("EZUtils/Editor Enhancements", isValidateFunction: false, priority: 0)]
        public static void ShowWindow()
        {
            EditorEnhancementsEditorWindow window = GetWindow<EditorEnhancementsEditorWindow>("Editor Enhancements");
            window.Show();
        }

        public void CreateGUI()
        {
            TranslateWindowTitle(this, "Editor Enhancements");

            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.EZUtils.EditorEnhancements/EditorEnhancementsEditorWindow.uxml");
            visualTree.CommonUIClone(rootVisualElement);
            TranslateElementTree(rootVisualElement);

            rootVisualElement.Q<Toolbar>().AddLocaleSelector();

            _ = rootVisualElement.Q<Toggle>(name: "projectWindowFileExtensions")
                .ForPref(ProjectWindowFileExtensions.PrefName, true)
                .RegisterValueChangedCallback(_ =>
                {
                    Type type = Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
                    EditorWindow window = GetWindow(type);
                    //without a repaint, the toggle wont do anything until the user interacts with it, like hover
                    window.Repaint();
                });

            _ = rootVisualElement.Q<Toggle>(name: "showSceneWindowOnPlay").ForPref(AutoSceneWindow.PrefName, true);

            rootVisualElement
                .Q<VisualElement>(className: "section-unityeditorlanguage")
                .Add(new EditorLanguageSettings());
        }
    }
}
