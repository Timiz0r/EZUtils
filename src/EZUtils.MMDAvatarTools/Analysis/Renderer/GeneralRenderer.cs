namespace EZUtils.MMDAvatarTools
{
    using UnityEngine.UIElements;

    public class GeneralRenderer : IAnalysisResultRenderer
    {
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
            container.Add(new Label("説明").WithClasses("results-details-title"));
            container.Add(new TextElement()
            {
                text = explanation
            }.WithClasses("analyzer-result-details-description"));

            if (instructions != null)
            {
                container.Add(new Label("修正方法").WithClasses("results-details-title"));
                container.Add(new TextElement()
                {
                    text = instructions
                }.WithClasses("analyzer-result-details-description"));
            }

            detailRenderer?.Render(container);
        }
    }
}
