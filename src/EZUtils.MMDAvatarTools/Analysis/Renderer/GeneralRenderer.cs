namespace EZUtils.MMDAvatarTools
{
    using UnityEngine.UIElements;

    public class GeneralRenderer : IAnalysisResultRenderer
    {
        private readonly string description;
        private readonly IAnalysisResultRenderer additionalRenderer;

        public GeneralRenderer(string description, IAnalysisResultRenderer additionalRenderer = null)
        {
            this.description = description;
            this.additionalRenderer = additionalRenderer;
        }

        public void Render(VisualElement container)
        {
            TextElement descriptionElement = new TextElement()
            {
                text = description
            };
            descriptionElement.AddToClassList("result-description");
            container.Add(descriptionElement);

            additionalRenderer?.Render(container);
        }
    }
}
