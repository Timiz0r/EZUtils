

namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VRC.SDK3.Avatars.Components;
    using static Localization;

    public class MmdAvatarAnalyzer
    {
        private readonly IReadOnlyList<IAnalyzer> analyzers;

        public MmdAvatarAnalyzer(IEnumerable<IAnalyzer> analyzers)
        {
            this.analyzers = analyzers.ToArray();
        }
        public MmdAvatarAnalyzer()
        {
            analyzers = new IAnalyzer[]
            {
                new BodyMeshAnalyzer(),
                new NonBodyMeshAnalyzer(),
                new EmptyStateAnalyzer(),
                new WriteDefaultsAnalyzer(),
                new Layer1And2Analyzer(),
                new HumanoidAnimationAnalyzer(),
                new BlendShapeSummaryAnalyzer(),
            };
        }

        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            List<AnalysisResult> results = new List<AnalysisResult>();
            foreach (IAnalyzer analyzer in analyzers)
            {
                try
                {
                    results.AddRange(analyzer.Analyze(avatar));
                }
                catch (Exception e) when (
                    ExceptionUtil.Record(() => results.Add(
                        new AnalysisResult(
                            new AnalysisResultIdentifier(
                                T($"An error has occurred in '{analyzer.GetType().Name}'."),
                                $"AnalyzerException.{analyzer.GetType().Name}"),
                            AnalysisResultLevel.AnalyzerError,
                            new GeneralRenderer(e.ToString())))))
                {
                }
            }
            return results;
        }
    }
}
