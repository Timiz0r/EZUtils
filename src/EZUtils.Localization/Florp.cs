namespace EZUtils.Localization
{
    public class Florp
    {
        private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder("Packages/com.timiz0r.ezutils.editorenhancements");


        public void Foo() => _ = loc.T("bar");
    }
}
