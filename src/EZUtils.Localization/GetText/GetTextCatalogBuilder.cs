namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;

    public class GetTextCatalogBuilder
    {
        //keys are paths, rooted or unrooted
        private ImmutableDictionary<string, GetTextDocumentBuilder> documents =
            ImmutableDictionary<string, GetTextDocumentBuilder>.Empty;

        public GetTextCatalogBuilder ForPoFile(
            string path,
            Locale locale,
            bool changeLocaleIfDifferent,
            Action<GetTextDocumentBuilder> documentBuilderAction)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (locale == null) throw new ArgumentNullException(nameof(locale));

            GetTextDocumentBuilder document = ImmutableInterlocked.GetOrAdd(ref documents, path, p => GetTextDocumentBuilder.ForDocumentAt(p, locale));
            _ = changeLocaleIfDifferent
                ? document.SetLocale(locale)
                : document.VerifyLocaleMatches(locale);

            documentBuilderAction?.Invoke(document);

            return this;
        }
        public GetTextCatalogBuilder ForPoFile(
            string path,
            Locale locale,
            Action<GetTextDocumentBuilder> documentBuilderAction)
            => ForPoFile(path, locale, changeLocaleIfDifferent: false, documentBuilderAction: documentBuilderAction);

        public GetTextCatalogBuilder ForEachDocument(Action<GetTextDocumentBuilder> documentBuilderAction)
        {
            foreach (GetTextDocumentBuilder documentBuilder in documents.Values)
            {
                documentBuilderAction?.Invoke(documentBuilder);
            }

            return this;
        }

        public GetTextCatalogBuilder WriteToDisk(string root = "")
        {
            foreach (GetTextDocumentBuilder doc in documents.Values)
            {
                _ = doc.WriteToDisk(root);
            }

            return this;
        }

        public GetTextCatalog GetCatalog(Locale nativeLocale)
            => new GetTextCatalog(GetDocuments(), nativeLocale ?? throw new ArgumentNullException(nameof(nativeLocale)));

        public IReadOnlyList<GetTextDocument> GetDocuments()
            => documents.Values.Select(db => db.GetGetTextDocument()).ToArray();
    }
}
