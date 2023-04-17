namespace EZUtils.Localization
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using UnityEditor;

    //TODO: shove extraction into a different assembly, so we dont have extraction running on users' projects

    //TODO: since we ended up going with roslyn, this gives us the opportunity to generate a proxy based on what EZLocalization looks like
    //we can generate it on domain reload and should be pretty deterministic. or we could play it safe and only write to the proxy if we see something isn't right.
    //it'll ideally be a two-pass thing. we'll generate the proxy, let domain reload happen, and extract (so that we can load up all the required syntax trees, including proxy)
    public class EZLocalizationExtractor
    {
        //TODO: ultimately want to do it per-assemblydefinition, so who knows what param we'll take
        public void ExtractFrom()
        {
            GetTextExtractor extractor = new GetTextExtractor(compilation => compilation
                .AddReferences(MetadataReference.CreateFromFile(typeof(EditorWindow).Assembly.Location)));
            //    .AddReferences(MetadataReference.CreateFromFile(typeof(VisualElement).Assembly.Location))
            void AddFile(string path) => extractor.AddFile(Path.GetFullPath(path), path);
            AddFile("Packages/com.timiz0r.ezutils.localization/ManualTestingEditorWindow.cs");
            AddFile("Packages/com.timiz0r.ezutils.localization/Florp.cs");
            AddFile("Packages/com.timiz0r.ezutils.localization/Localization.cs");

            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            extractor.Extract(catalogBuilder);
            _ = catalogBuilder
                .ForEachDocument(d => d
                    .ForEachEntry(e => e.GetCompatibilityVersion())
                    .SortEntries(EntryComparer.Instance))
                .WriteToDisk("Packages/com.timiz0r.ezutils.localization");
        }

        private class EntryComparer : IComparer<GetTextEntry>
        {
            public static EntryComparer Instance { get; } = new EntryComparer();
            public int Compare(GetTextEntry x, GetTextEntry y)
                => StringComparer.OrdinalIgnoreCase.Compare(
                    x.Header.References.FirstOrDefault() ?? string.Empty,
                    y.Header.References.FirstOrDefault() ?? string.Empty);
        }
    }
}
