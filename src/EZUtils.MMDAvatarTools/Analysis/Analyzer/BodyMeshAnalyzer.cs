

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
                ResultCode.NoBody,
                AnalysisResultLevel.Error,
                null);
            if (body.TryGetComponent(out SkinnedMeshRenderer bodyMesh))
            {
                return MmdBlendShapeSummary.Generate(bodyMesh).HasAnyMmdBlendShapes
                    ? AnalysisResult.Generate(
                        ResultCode.MmdBodyMeshFound,
                        AnalysisResultLevel.Pass,
                        null)
                    : AnalysisResult.Generate(
                        ResultCode.BodyHasNoMmdBlendShapes,
                        AnalysisResultLevel.Error, //users probably want their face to move, so an error seems more appropriate
                        null);
            }

            if (body.GetComponent<MeshFilter>() != null) return AnalysisResult.Generate(
                ResultCode.NotSkinnedMeshRenderer,
                AnalysisResultLevel.Error,
                null);

            //so we have a body but no renderer
            return AnalysisResult.Generate(
                ResultCode.NoRendererInBody,
                AnalysisResultLevel.Error,
                null);
        }

        public static class ResultCode
        {
            public static readonly string MmdBodyMeshFound = Code();
            public static readonly string NoBody = Code();
            public static readonly string NotSkinnedMeshRenderer = Code();
            public static readonly string NoRendererInBody = Code();
            public static readonly string BodyHasNoMmdBlendShapes = Code();

            private static string Code([CallerMemberName] string caller = "")
                => $"{nameof(BodyMeshAnalyzer)}.{caller}";
        }
    }
}
