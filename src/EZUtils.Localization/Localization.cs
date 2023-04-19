namespace EZUtils.Localization.Proxy
{
    using System;

    [GenerateLanguage("ja", "ja.po", UseSpecialZero = true, Other = " @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")]
    [GenerateLanguage("ko", "ko.po", UseSpecialZero = true, Other = " @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")]
    public static class Localization
    {
        private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder("Packages/com.timiz0r.ezutils.localization", "EZUtils");
        [LocalizationMethod]
        public static string T(RawString id) => loc.T(id);
        [LocalizationMethod]
        public static string T(FormattableString id) => loc.T(id);
    }
}
