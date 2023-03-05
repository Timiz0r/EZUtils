namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using VRC.SDK3.Avatars.Components;

    public class BlendShapeSummaryAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            MmdBlendShapeSummary blendShapeSummary = MmdBlendShapeSummary.Generate(avatar);

            return AnalysisResult.Create(
                Result.BlendShapeSummary,
                AnalysisResultLevel.Informational,
                new GeneralRenderer(
                    "MMD対応のブレンドシェープで表情が変化できます。",
                    detailRenderer: new BlendShapeSummaryRenderer(blendShapeSummary)));
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier BlendShapeSummary =
                AnalysisResultIdentifier.Create<BlendShapeSummaryAnalyzer>("MMD対応のブレンドシェープ");
        }
    }
}
