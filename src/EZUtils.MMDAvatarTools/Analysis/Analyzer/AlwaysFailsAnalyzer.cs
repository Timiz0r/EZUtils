namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using VRC.SDK3.Avatars.Components;

    public class AlwaysFailsAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar) => throw new System.NotSupportedException();
    }
}
