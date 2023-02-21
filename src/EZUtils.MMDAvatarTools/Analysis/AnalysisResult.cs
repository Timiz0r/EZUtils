

namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;

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

        public static IReadOnlyList<AnalysisResult> Generate(
            string resultCode, AnalysisResultLevel level, IAnalysisResultRenderer renderer)
            => new[] { new AnalysisResult(resultCode, level, renderer) };
    }
}
