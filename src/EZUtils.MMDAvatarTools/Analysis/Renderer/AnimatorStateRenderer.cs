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
            container.Add(new Label(title));

            VisualElement statesContainer = new VisualElement();
            // objectContainer.AddToClassList("result-objects");
            container.Add(statesContainer);

            foreach (IGrouping<string, string> group in states.GroupBy(s => s.layerName, s => s.layerName))
            {
                VisualElement layerContainer = new VisualElement();
                statesContainer.Add(layerContainer);

                VisualElement layerName = new VisualElement();
                layerName.AddToClassList("result-details-state-layername");
                layerName.Add(new Label($"レイヤー 「{group.Key}」"));
                layerName.Add(new Button(() => FocusAnimatorControllerLayer(group.Key)) { text = "開く" });
                layerContainer.Add(layerName);

                VisualElement stateNames = new VisualElement();
                stateNames.AddToClassList("result-details-state-statenames");
                foreach (string stateName in group)
                {
                    stateNames.Add(new Label(stateName));
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
