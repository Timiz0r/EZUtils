namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.CodeAnalysis;

    public class GetTextCatalogBuilder
    {
        //keys are paths, rooted or unrooted
        private readonly Dictionary<string, GetTextDocumentBuilder> documents =
            new Dictionary<string, GetTextDocumentBuilder>();

        public GetTextCatalogBuilder ForPoFile(
            string path, Locale locale, Action<GetTextDocumentBuilder> documentBuilderAction)
        {
            if (documents.TryGetValue(path, out GetTextDocumentBuilder document))
            {
                _ = document.VerifyLocaleMatches(locale);
                documentBuilderAction(document);

                return this;
            }

            document = documents[path] = GetTextDocumentBuilder.ForDocumentAt(path, locale);
            documentBuilderAction(document);

            return this;
        }

        public GetTextCatalogBuilder ForEachDocument(Action<GetTextDocumentBuilder> documentBuilderAction)
        {
            foreach (GetTextDocumentBuilder documentBuilder in documents.Values)
            {
                documentBuilderAction(documentBuilder);
            }

            return this;
        }

        public GetTextCatalogBuilder WriteToDisk(string root)
        {
            foreach (GetTextDocumentBuilder doc in documents.Values)
            {
                _ = doc.WriteToDisk(root);
            }

            return this;
        }

        public GetTextCatalog GetCatalog(Locale nativeLocale)
            => new GetTextCatalog(
                documents.Values.Select(db => db.GetGetTextDocument()).ToArray(),
                nativeLocale);
    }
}
