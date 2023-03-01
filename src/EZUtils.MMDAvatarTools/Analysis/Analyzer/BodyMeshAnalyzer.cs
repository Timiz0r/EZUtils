

namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    public class BodyMeshAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            Transform body = avatar.transform.Find("Body");
            //will want to output these eventually, so not a bool
            //while the nonbodymesh analyzer includes inactive, we don't here because we're trying to detect the main
            //meshes, which are likely always on.
            SkinnedMeshRenderer[] otherMeshesWithMMDBlendShapes = avatar
                .GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(smr => MmdBlendShapeSummary.Generate(smr).Results.Any(r => r.ExistingBlendShapes.Any()))
                .ToArray();

            if (body == null) return AnalysisResult.Generate(
                Result.NoBody,
                AnalysisResultLevel.Error,
                null);
            if (body.TryGetComponent(out SkinnedMeshRenderer bodyMesh))
            {
                return MmdBlendShapeSummary.Generate(bodyMesh).HasAnyMmdBlendShapes
                    ? AnalysisResult.Generate(
                        Result.MmdBodyMeshFound,
                        AnalysisResultLevel.Pass,
                        null)
                    : AnalysisResult.Generate(
                        Result.BodyHasNoMmdBlendShapes,
                        AnalysisResultLevel.Error, //users probably want their face to move, so an error seems more appropriate
                        null);
            }

            if (body.GetComponent<MeshFilter>() != null) return AnalysisResult.Generate(
                Result.NotSkinnedMeshRenderer,
                AnalysisResultLevel.Error,
                null);

            //so we have a body but no renderer
            return AnalysisResult.Generate(
                Result.NoRendererInBody,
                AnalysisResultLevel.Error,
                null);
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier MmdBodyMeshFound =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>("MMD対応のBodyというメッシュを発見");
            public static readonly AnalysisResultIdentifier NoBody =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>("Bodyというゲームオブジェクトが未発見");
            public static readonly AnalysisResultIdentifier NotSkinnedMeshRenderer =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>("BodyというメッシュはSkinned Mesh Rendererではありません");
            public static readonly AnalysisResultIdentifier NoRendererInBody =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>("BodyというゲームオブジェクトにはRendererはありません");
            public static readonly AnalysisResultIdentifier BodyHasNoMmdBlendShapes =
                AnalysisResultIdentifier.Create<BodyMeshAnalyzer>("BodyというメッシュにはMMD対応のブレンドシェープはありません");
        }
    }
}
