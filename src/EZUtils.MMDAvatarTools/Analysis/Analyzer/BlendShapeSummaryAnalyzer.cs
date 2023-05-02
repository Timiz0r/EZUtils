namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using VRC.SDK3.Avatars.Components;
    using static Localization;

    public class BlendShapeSummaryAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            MmdBlendShapeSummary blendShapeSummary = MmdBlendShapeSummary.Generate(avatar);

            return AnalysisResult.Create(
                Result.BlendShapeSummary,
                AnalysisResultLevel.Informational,
                new GeneralRenderer(
                    T("Facial expressions can be changed when MMD-compatible blendshapes are present."),
                    detailRenderer: new BlendShapeSummaryRenderer(blendShapeSummary)));
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier BlendShapeSummary =
                AnalysisResultIdentifier.Create<BlendShapeSummaryAnalyzer>(T("MMD-compatible blendshapes"));
        }
    }
}
