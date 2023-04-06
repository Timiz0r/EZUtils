namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    public class GetTextCatalog
    {
        private readonly IReadOnlyList<GetTextDocument> documents;
        private readonly CultureInfo nativeLocale;
        private GetTextDocument selectedDocument = null;

        public GetTextCatalog(IReadOnlyList<GetTextDocument> documents, CultureInfo nativeLocale)
        {
            this.documents = documents;
            this.nativeLocale = nativeLocale;
        }
        public string T(string id)
            => selectedDocument?.Entries
                ?.SingleOrDefault(e => e.Context == null && e.PluralId == null && e.Id == id)
                ?.Value ?? id;

        public bool Supports(CultureInfo locale) => locale == nativeLocale || documents.Any(d => d.Header.Locale == locale);

        public void SelectLocale(CultureInfo locale) => selectedDocument =
            locale == nativeLocale
                ? null
                : documents.SingleOrDefault(d => d.Header.Locale == locale)
                    ?? throw new ArgumentOutOfRangeException(
                        nameof(locale),
                        $"This catalog does not support the locale '{locale}'. " +
                        $"Supported locales: {string.Join(", ", documents.Select(d => d.Header.Locale))}");
    }
}
