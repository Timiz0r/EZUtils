namespace EZUtils.MMDAvatarTools
{
    using UnityEditor;
    using UnityEngine.UIElements;
    using static Localization;

    public class GeneralRenderer : IAnalysisResultRenderer
    {
        private readonly VisualTreeAsset layerElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Packages/com.timiz0r.ezutils.mmdavatartools/Analysis/Renderer/GeneralRenderer.uxml");

        private readonly string explanation;
        private readonly string instructions;
        private readonly IAnalysisResultRenderer detailRenderer;

        public GeneralRenderer(
            string explanation,
            string instructions = null,
            IAnalysisResultRenderer detailRenderer = null)
        {
            this.explanation = explanation;
            this.instructions = instructions;
            this.detailRenderer = detailRenderer;
        }

        public void Render(VisualElement container)
        {
            VisualElement element = layerElement.CommonUIClone();
            container.Add(element);
            TranslateElementTree(element);

            element.Q<TextElement>(name: "explanation").text = explanation;
            if (instructions != null)
            {
                element.Q<TextElement>(name: "fix").text = explanation;
            }

            detailRenderer?.Render(container);
        }
    }
}
