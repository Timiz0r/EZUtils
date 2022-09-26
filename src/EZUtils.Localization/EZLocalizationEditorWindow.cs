namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.Localization.Settings;
    using UnityEngine.UIElements;

    public class EZLocalizationEditorWindow : EditorWindow
    {
        private readonly List<EZLocalization> localizations = new List<EZLocalization>();
        [MenuItem("EZUtils/EZLocalization editing", isValidateFunction: false, priority: 0)]
        public static void ShowWindow()
        {
            EZLocalizationEditorWindow window = GetWindow<EZLocalizationEditorWindow>("EZLocalization");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.localization/EZLocalizationEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            Button enterEditModeButton = rootVisualElement.Q<Button>(name: "enterEditMode");
            rootVisualElement.Q<Button>(name: "enterEditMode").clicked += () =>
            {
                EZLocalization target = rootVisualElement.Q<PopupField<EZLocalization>>().value;
                target?.EnterEditMode();
            };

            rootVisualElement.Q<Button>(name: "stopEditing").clicked += () => EZLocalization.StopEditing();

            rootVisualElement.Q<Button>(name: "refresh").clicked += () => InitializeSelectorPopup();
            InitializeSelectorPopup();
        }

        private void InitializeSelectorPopup()
        {
            localizations.Clear();
            localizations.AddRange(EZLocalization.InitializedLocalizations);

            VisualElement container = rootVisualElement.Q<VisualElement>(className: "localization-picker-container");
            container.Clear();

            if (localizations.Count == 0)
            {
                container.Add(new PopupField<EZLocalization>());
            }
            else
            {
                PopupField<EZLocalization> targetLocalizationPopup = new PopupField<EZLocalization>(
                    localizations,
                    localizations.FirstOrDefault(),
                    l => l.Name,
                    l => l.Name);
                container.Add(targetLocalizationPopup);
            }

        }
    }
}
