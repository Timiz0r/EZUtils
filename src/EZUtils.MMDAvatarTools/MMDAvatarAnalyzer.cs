

namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VRC.SDK3.Avatars.Components;

    public class MMDAvatarAnalyzer
    {
        private readonly IReadOnlyList<IAnalyzer> analyzers;

        public MMDAvatarAnalyzer(IEnumerable<IAnalyzer> analyzers)
        {
            this.analyzers = analyzers.ToArray();
        }
        public MMDAvatarAnalyzer()
        {
            analyzers = new[]
            {
                new BodyMeshExistsAnalyzer()
            };
        }

        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            List<AnalysisResult> results = new List<AnalysisResult>(analyzers.Count);
            foreach (IAnalyzer analyzer in analyzers)
            {
                results.AddRange(analyzer.Analyze(avatar));
            }
            return results;
        }
    }

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
                ResultCode.Pass,
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
            public static readonly string Pass = Code();
            public static readonly string NoBody = Code();
            public static readonly string NotSkinnedMeshRenderer = Code();
            public static readonly string NoRendererInBody = Code();

            private static string Code([CallerMemberName] string caller = "")
                => $"{nameof(BodyMeshExistsAnalyzer)}.{caller}";
        }
    }

    public interface IAnalyzer
    {
        IEnumerable<AnalysisResult> Analyze(VRCAvatarDescriptor avatar);
        //maybe later members for remediation
    }

    public class AnalysisResult
    {
        //design-wise, want the analyzers in MMDAvatarAnalyzer to be transparent to driver ports, in general
        //yet, the unit test driver adapters need to accurately identify the failure
        //otherwise, maybe the intended failure is passing, and another is coincidentally failing
        //
        //in a future version, could also go strongly-typed results via inheritance
        //would work well with pattern matching and records/DUs, but kinda sucks without these language features
        public string ResultCode { get; }

        public AnalysisResultLevel Level { get; }

        public IAnalysisResultRenderer Renderer { get; }

        public AnalysisResult(string resultCode, AnalysisResultLevel level, IAnalysisResultRenderer renderer)
        {
            ResultCode = resultCode;
            Level = level;
            Renderer = renderer;
        }

        public static IEnumerable<AnalysisResult> Generate(
            string resultCode, AnalysisResultLevel level, IAnalysisResultRenderer renderer)
            => Enumerable.Repeat(new AnalysisResult(resultCode, level, renderer), 1);
    }

    public enum AnalysisResultLevel
    {
        Pass,
        Informational,
        Warning,
        Error,
        AnalyzerError
    }

    public interface IAnalysisResultRenderer
    {
        void Render(VisualElement container);
    }
}
