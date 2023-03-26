namespace EZUtils.MMDAvatarTools
{
    using System.Runtime.CompilerServices;

    public class AnalysisResultIdentifier
    {
        //TODO: will eventually be localized
        //this could hypothetically have been paired with renderer, which also would need to be localized
        public string FriendlyName { get; }

        //where this will not be localized
        public string Code { get; }

        public AnalysisResultIdentifier(string friendlyName, string code)
        {
            FriendlyName = friendlyName;
            Code = code;
        }

        public static AnalysisResultIdentifier Create<T>(string friendlyName, [CallerMemberName] string caller = "") where T : IAnalyzer
            => new AnalysisResultIdentifier(friendlyName, code: $"{typeof(T).Name}.{caller}");
    }
}
