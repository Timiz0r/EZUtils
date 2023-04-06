namespace EZUtils.Localization
{
    using System.Globalization;

    public class CatalogReference
    {
        private CultureInfo selectedLocale;

        public GetTextCatalog Catalog { get; private set; }
        public CultureInfo NativeLocale { get; }

        public CatalogReference(CultureInfo nativeLocale)
        {
            NativeLocale = selectedLocale = nativeLocale;
        }

        public void UseUpdatedCatalog(GetTextCatalog catalog)
        {
            Catalog = catalog;
            Catalog.SelectLocale(selectedLocale);
        }

        public void SelectLocale(CultureInfo locale)
        {
            selectedLocale = locale;
            Catalog.SelectLocale(locale);
        }
    }
}
