namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;
    using static Localization;

    public class NonBodyMeshAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            //include inactive since toggling meshes is a thing. we're tring to detect attempts at making non-main
            //meshes react to mmd shapekeys, which is impossible, as well as "main" meshes with a wrong name.
            SkinnedMeshRenderer[] nonBodyMeshesWithMmdBlendShapes =
                avatar.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true)
                    .Where(smr => !smr.name.Equals("Body", StringComparison.OrdinalIgnoreCase)
                        && MmdBlendShapeSummary.Generate(smr).HasAnyMmdBlendShapes)
                    .ToArray();

            return nonBodyMeshesWithMmdBlendShapes.Length == 0
                ? AnalysisResult.Create(Result.ClearOfMMDBlendShapes, AnalysisResultLevel.Pass, new EmptyRenderer())
                : AnalysisResult.Create(
                    Result.ContainsMMDBlendShapes,
                    AnalysisResultLevel.Warning,
                    new GeneralRenderer(
                        T("Non-body meshes will not be animated."),
                        instructions:
                            T("If the below meshes must be used, merge them with the Body mesh."),
                        detailRenderer: ObjectSelectionRenderer.Create(
                            listTitle: T("Meshes containing MMD-compatible blendshapes"),
                            emptyMessage: T("There are no meshes containing MMD-compatible blendshapes."),
                            objects: nonBodyMeshesWithMmdBlendShapes)));
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier ContainsMMDBlendShapes =
                AnalysisResultIdentifier.Create<NonBodyMeshAnalyzer>(
                    T("There are non-Body meshes with MMD-compatible blendshapes"));
            public static readonly AnalysisResultIdentifier ClearOfMMDBlendShapes =
                AnalysisResultIdentifier.Create<NonBodyMeshAnalyzer>(
                    T("Non-Body meshes have no MMD-compatible blendshapes"));
        }
    }
}
