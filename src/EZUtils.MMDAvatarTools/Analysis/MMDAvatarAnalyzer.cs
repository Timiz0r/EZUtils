

namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using VRC.SDK3.Avatars.Components;

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
                new EmptyStateAnalyzer()
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
}
