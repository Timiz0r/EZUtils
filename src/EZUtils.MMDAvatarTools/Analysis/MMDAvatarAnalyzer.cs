

namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
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
            analyzers = new IAnalyzer[]
            {
                new BodyMeshExistsAnalyzer(),
                new NonBodyMeshAnalyzer()
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
