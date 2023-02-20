

namespace EZUtils.MMDAvatarTools
{
    using System;
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
            SkinnedMeshRenderer skinnedMeshRenderer = body.GetComponent<SkinnedMeshRenderer>();

            AnalysisResult result = new AnalysisResult(
                ResultCode.Pass,
                level: skinnedMeshRenderer == null ? AnalysisResultLevel.Error : AnalysisResultLevel.Pass,
                renderer: null);
            return Enumerable.Repeat(result, 1);
        }

        public static class ResultCode
        {
            public static readonly string Pass = Code();
            public static readonly string NoBody = Code();
            public static readonly string NotSkinnedMeshRenderer = Code();

            private static string Code([CallerMemberName] string caller = "")
                => $"{nameof(BodyMeshExistsAnalyzer)}.{caller}";
        }
    }

    public interface IAnalyzer
    {
        IEnumerable<AnalysisResult> Analyze(VRCAvatarDescriptor avatar);
    }

    public class AnalysisResult
    {
        //design-wise, want the analyzers in MMDAvatarAnalyzer to be transparent to driver ports, in general
        //yet, the unit test driver adapters need to accurately identify the failure
        //otherwise, maybe the intended failure is passing, and another is coincidentally failing
        public string ResultCode { get; }

        public AnalysisResultLevel Level { get; }

        public IAnalysisResultRenderer Renderer { get; }

        public AnalysisResult(string resultCode, AnalysisResultLevel level, IAnalysisResultRenderer renderer)
        {
            ResultCode = resultCode;
            Level = level;
            Renderer = renderer;
        }
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
