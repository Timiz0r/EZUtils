namespace EZUtils.Localization.Proxy
{
    using System;

    [GenerateLanguage("ja", "ja.po", Other = "")]
    [GenerateLanguage("ko", "ko.po", Other = "")]
    public static class Localization
    {
        private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder("Packages/com.timiz0r.ezutils.editorenhancements");
        [LocalizationMethod]
        public static string T(RawString id) => loc.T(id);
        [LocalizationMethod]
        public static string T(FormattableString id) => loc.T(id);
    }
}
