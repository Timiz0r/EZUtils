namespace EZUtils.MMDAvatarTools
{
    using System.Runtime.CompilerServices;
    using EZUtils.Localization;
    using static Localization;

    [GenerateLanguage("en", "template.pot", One = "", Other = "")]
    [GenerateLanguage("ja", "ja.po", Other = " @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")]
    public class AnalysisResultIdentifier
    {
        private readonly string nativeFriendlyName;

        public string FriendlyName => T(nativeFriendlyName);

        //where this will not be localized
        public string Code { get; }

        public AnalysisResultIdentifier(string friendlyName, string code)
        {
            nativeFriendlyName = friendlyName;
            Code = code;
        }

        [LocalizationMethod]
        public static AnalysisResultIdentifier Create<T>(
            [LocalizationParameter(LocalizationParameter.Id)] string friendlyName,
            [CallerMemberName] string caller = "") where T : IAnalyzer
            => new AnalysisResultIdentifier(friendlyName, code: $"{typeof(T).Name}.{caller}");
    }
}
