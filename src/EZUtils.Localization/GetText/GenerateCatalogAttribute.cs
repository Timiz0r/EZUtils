namespace EZUtils.Localization
{
    using System;

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateCatalogAttribute : Attribute
    {
        public string CatalogRoot { get; }

        public GenerateCatalogAttribute(string catalogRoot)
        {
            CatalogRoot = catalogRoot;
        }
    }
}
