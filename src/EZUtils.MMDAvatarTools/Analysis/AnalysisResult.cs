namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;

    public class AnalysisResult
    {
        //design-wise, want the analyzers in MMDAvatarAnalyzer to be transparent to driver ports, in general
        //yet, the unit test driver adapters need to accurately identify the failure
        //otherwise, maybe the intended failure is passing, and another is coincidentally failing
        //
        //in a future version, could also go strongly-typed results via inheritance
        //would work well with pattern matching and records/DUs, but kinda sucks without these language features
        public AnalysisResultIdentifier Result { get; }
        //TODO:
        //at time of writing, levels dont vary based on result, but this isn't necessarily the design.
        //doing so might simplify things, since we can statically define everything underone umbrella,
        //instead of potentially two: this class and the identifier class.
        //statically defining this is handy for localization. the general plan with localization is to add entries
        //to tables as we render, but this doesn't work as well for analyzers which may not be hit (well, tests aside).
        //
        //static definition is hard when taking into account dynamic things, like a list of layers and states.
        //how do we get the button to jump to the layer to show up in the table without doing non-"ez" things.
        //we'll save this design consideration of later, and, back on topic, the design around this class as well.
        public AnalysisResultLevel Level { get; }

        public IAnalysisResultRenderer Renderer { get; }

        public AnalysisResult(
            AnalysisResultIdentifier result, AnalysisResultLevel level, IAnalysisResultRenderer renderer)
        {
            Result = result;
            Level = level;
            Renderer = renderer;
        }

        public static IReadOnlyList<AnalysisResult> Generate(
            AnalysisResultIdentifier result, AnalysisResultLevel level, IAnalysisResultRenderer renderer)
            => new[] { new AnalysisResult(result, level, renderer) };
    }
}
