

namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    public class BodyMeshExistsAnalyzer : IAnalyzer
    {
        public IEnumerable<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            Transform body = avatar.transform.Find("Body");
            if (body == null) return AnalysisResult.Generate(
                ResultCode.NoBody,
                AnalysisResultLevel.Error,
                null); ;

            if (body.GetComponent<SkinnedMeshRenderer>() != null) return AnalysisResult.Generate(
                ResultCode.BodySkinnedMeshExists,
                AnalysisResultLevel.Pass,
                null);

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
            public static readonly string BodySkinnedMeshExists = Code();
            public static readonly string NoBody = Code();
            public static readonly string NotSkinnedMeshRenderer = Code();
            public static readonly string NoRendererInBody = Code();

            private static string Code([CallerMemberName] string caller = "")
                => $"{nameof(BodyMeshExistsAnalyzer)}.{caller}";
        }
    }
}
