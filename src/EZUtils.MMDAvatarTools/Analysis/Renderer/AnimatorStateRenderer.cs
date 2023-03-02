namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class AnimatorStateRenderer : IAnalysisResultRenderer
    {
        private readonly VisualTreeAsset layerElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Packages/com.timiz0r.ezutils.mmdavatartools/Analysis/Renderer/AnimatorStateRendererLayerElement.uxml");
        private readonly string title;
        private readonly AnimatorController animatorController;
        private readonly IReadOnlyList<(string layerName, string stateName)> states;

        public AnimatorStateRenderer(
            string title,
            AnimatorController animatorController,
            IReadOnlyList<(string layerName, string stateName)> states)
        {
            this.title = title;
            this.animatorController = animatorController;
            this.states = states;
        }

        public void Render(VisualElement container)
        {
            container.Add(new Label(title).WithClasses("results-details-title"));

            VisualElement statesContainer = new VisualElement();
            container.Add(statesContainer);

            foreach (IGrouping<string, string> group in states.GroupBy(s => s.layerName, s => s.layerName))
            {
                TemplateContainer layerContainer = layerElement.CloneTree();
                statesContainer.Add(layerContainer);

                layerContainer.Q<Label>(className: "analyzer-result-details-state-layer-name").text = group.Key;
                layerContainer.Q<Button>().clicked += () => FocusAnimatorControllerLayer(group.Key);

                VisualElement stateNameContainer = layerContainer.Q<VisualElement>(className: "analyzer-result-details-state-list");
                foreach (string stateName in group)
                {
                    stateNameContainer.Add(new Label(stateName));
                }
            }
        }

        private void FocusAnimatorControllerLayer(string layerName)
        {
            int layerIndex = Array.FindIndex(animatorController.layers, l => l.name == layerName);
            if (layerIndex < 0)
            {
                Debug.LogError($"Unable to find layer '{layerName}' in controller '{animatorController}'. Layers available: {string.Join(", ", animatorController.layers.Select(l => l.name))}.");
                return;
            }

            Type animatorType = Type.GetType("UnityEditor.Graphs.AnimatorControllerTool, UnityEditor.Graphs");
            EditorWindow window = EditorWindow.GetWindow(animatorType);

            animatorType.GetProperty("animatorController").SetValue(window, animatorController);
            animatorType.GetProperty("selectedLayerIndex").SetValue(window, layerIndex);
        }
    }
}
