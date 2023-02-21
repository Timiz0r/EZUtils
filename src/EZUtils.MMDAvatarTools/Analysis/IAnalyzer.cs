

namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using VRC.SDK3.Avatars.Components;

    public interface IAnalyzer
    {
        IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar);
        //maybe later members for remediation
    }
}
