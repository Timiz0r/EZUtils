namespace EZUtils.Localization.Proxy
{
    using System;

    [GenerateCatalog("Packages/com.timiz0r.ezutils.editorenhancements")]
    [GenerateLanguage("jp", Other = "")]
    [GenerateLanguage("ko", Other = "")]
    public static class Localization
    {
        private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder("Packages/com.timiz0r.ezutils.editorenhancements");
        [LocalizationMethod]
        public static string T(RawString id) => loc.T(id);
        [LocalizationMethod]
        public static string T(FormattableString id) => loc.T(id);
    }
}
