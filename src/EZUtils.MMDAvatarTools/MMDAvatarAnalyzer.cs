

namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
                typeof(BodyMeshExistsAnalyzer),
                renderer: null,
                level: skinnedMeshRenderer == null ? AnalysisResultLevel.Error : AnalysisResultLevel.Pass);
            return Enumerable.Repeat(result, 1);
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
        public Type AnalyzerType { get; }

        public AnalysisResultLevel Level { get; }

        public IAnalysisResultRenderer Renderer { get; }

        public AnalysisResult(Type analyzerType, AnalysisResultLevel level, IAnalysisResultRenderer renderer)
        {
            AnalyzerType = analyzerType;
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
