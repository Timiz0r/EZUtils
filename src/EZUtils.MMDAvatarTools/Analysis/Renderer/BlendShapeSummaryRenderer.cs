namespace EZUtils.MMDAvatarTools
{
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class BlendShapeSummaryRenderer : IAnalysisResultRenderer
    {
        private readonly VisualTreeAsset itemElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
            "Packages/com.timiz0r.ezutils.mmdavatartools/Analysis/Renderer/BlendShapeSummaryItem.uxml");
        private readonly MmdBlendShapeSummary blendShapeSummary;

        public BlendShapeSummaryRenderer(MmdBlendShapeSummary blendShapeSummary)
        {
            this.blendShapeSummary = blendShapeSummary;
        }

        public void Render(VisualElement container)
        {
            foreach (IGrouping<BlendShapeTarget, BlendShapeSummaryResult> group in blendShapeSummary.Results
                .OrderBy(r => r.Definition.Target)
                .ThenByDescending(r => r.Definition.IsCommon)
                .GroupBy(r => r.Definition.Target))
            {
                //this will be the awkward bit of english, but it's not worth hard-coding in translations atm
                //since we'll add localization support later
                Label groupTitle = new Label(group.Key.ToString());
                groupTitle.style.fontSize = 16;
                groupTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                container.Add(groupTitle);

                foreach (BlendShapeSummaryResult result in group)
                {
                    VisualElement item = itemElement.CommonUIClone();
                    container.Add(item);

                    VisualElement status =
                        item.Q<VisualElement>(className: "analyzer-result-details-blendshape-status");
                    if (result.ExistingBlendShapes.Count > 0)
                    {
                        status.AddToClassList("analyzer-result-details-blendshape-status-exists");
                    }

                    Label mainTerm = item.Q<Label>(className: "analyzer-result-details-blendshape-term-main");
                    mainTerm.text = result.Definition.Name;
                    if (result.ExistingBlendShapes.Contains(result.Definition.Name))
                    {
                        mainTerm.AddToClassList("analyzer-result-details-blendshape-term-exists");
                    }

                    VisualElement allTermsContainer =
                        item.Q<VisualElement>(className: "analyzer-result-details-blendshape-term-all");
                    foreach (string possibleName in result.Definition.PossibleNames)
                    {
                        if (possibleName == result.Definition.Name) continue;

                        Label currentTerm = new Label(possibleName);
                        allTermsContainer.Add(currentTerm);
                        if (result.ExistingBlendShapes.Contains(possibleName))
                        {
                            currentTerm.AddToClassList("analyzer-result-details-blendshape-term-exists");
                        }
                    }
                }
            }
        }
    }
}
