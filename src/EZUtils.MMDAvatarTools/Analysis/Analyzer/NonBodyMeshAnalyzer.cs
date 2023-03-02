namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    public class NonBodyMeshAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            //include inactive since toggling meshes is a thing. we're tring to detect attempts at making non-main
            //meshes react to mmd shapekeys, which is impossible, as well as "main" meshes with a wrong name.
            SkinnedMeshRenderer[] nonBodyMeshesWithMMdBlendShapes =
                avatar.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true)
                    .Where(smr => !smr.name.Equals("Body", StringComparison.OrdinalIgnoreCase)
                        && MmdBlendShapeSummary.Generate(smr).HasAnyMmdBlendShapes)
                    .ToArray();

            return nonBodyMeshesWithMMdBlendShapes.Length == 0
                ? AnalysisResult.Create(Result.ClearOfMMDBlendShapes, AnalysisResultLevel.Pass, new EmptyRenderer())
                : AnalysisResult.Create(
                    Result.ContainsMMDBlendShapes,
                    AnalysisResultLevel.Warning,
                    new GeneralRenderer(
                        "Bodyではないメッシュなんですから、変化しません。",
                        ObjectSelectionRenderer.Create(
                            "MMD対応のブレンドシェープがあるメッシュ", nonBodyMeshesWithMMdBlendShapes)));
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier ContainsMMDBlendShapes =
                AnalysisResultIdentifier.Create<NonBodyMeshAnalyzer>(
                    "Bodyではないメッシュには、MMD対応のブレンドシェープがあります");
            public static readonly AnalysisResultIdentifier ClearOfMMDBlendShapes =
                AnalysisResultIdentifier.Create<NonBodyMeshAnalyzer>(
                    "Bodyではないメッシュには、MMD対応のブレンドシェープはありません");
        }
    }
}
