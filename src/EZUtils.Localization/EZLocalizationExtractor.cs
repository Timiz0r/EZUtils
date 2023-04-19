namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.CodeAnalysis;
    using UnityEditor;

    //TODO: shove extraction into a different assembly, so we dont have extraction running on users' projects
    //TODO: improve sorting of entries
    //  first, entries with more references are probably more important and should go further up
    //  for entries with same number of references, could try to nominate the candidate reference based on commonality of directory, so to speak
    //  next, we'll want to split line number from path, then sort candidate reference by them

    //TODO: since we ended up going with roslyn, this gives us the opportunity to generate a proxy based on what EZLocalization looks like
    //we can generate it on domain reload and should be pretty deterministic. or we could play it safe and only write to the proxy if we see something isn't right.
    //it'll ideally be a two-pass thing. we'll generate the proxy, let domain reload happen, and extract (so that we can load up all the required syntax trees, including proxy)
    //actually, since we'll move extraction into a separate package, there too go the roslyn libs
    //instead, we'll just go template-style
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
                    .Prune()
                    .ForEachEntry(e => GetCompatibilityVersion(e))
                    .SortEntries(EntryComparer.Instance))
                .WriteToDisk("Packages/com.timiz0r.ezutils.localization");
        }

        //TODO: eventually, if we have an editor that can support our more forgiving handling, we want the extraction
        //merge to refill in automated mid-entry comments. at time of writing, this is just plural rule comments
        public GetTextEntry GetCompatibilityVersion(GetTextEntry entry)
        {
            //is likely better than one or two extra array allocations, plus whatever allocations are involved
            if (IsAlreadyCompatible()) return entry;

            //capacity would double if there's an inline comment, and expand no more in that case
            List<GetTextLine> newLines = new List<GetTextLine>(entry.Lines.Count);
            bool doneWithInitialWhitespace = false;
            foreach (GetTextLine line in entry.Lines)
            {
                if (line.IsWhiteSpace && !doneWithInitialWhitespace)
                {
                    newLines.Add(line);
                    continue;
                }
                //we make sure all lines in an entry are not separated by whitespace
                //we could turn them into comments, but it would conflict weirdly with wiping out mid-entry comments
                doneWithInitialWhitespace = true;

                if (line.IsComment && !line.IsMarkedObsolete)
                {
                    newLines.Add(line);
                }
            }
            foreach (GetTextLine line in entry.Lines)
            {
                if (line.IsCommentOrWhiteSpace) continue;

                //aka inline comment
                if (line.Comment is string comment)
                {
                    newLines.Add(new GetTextLine(comment: comment));
                }
            }
            foreach (GetTextLine line in entry.Lines)
            {
                if (line.IsMarkedObsolete)
                {
                    newLines.Add(line);
                }
                else if (!line.IsCommentOrWhiteSpace)
                {
                    newLines.Add(line.Comment == null ? line : new GetTextLine(line.Keyword, line.StringValue));
                }
            }

            //could parse, but our transformation should be the same as `this`, except for lines
            GetTextEntry newEntry = new GetTextEntry(
                lines: newLines,
                header: entry.Header,
                isObsolete: entry.IsObsolete,
                context: entry.Context,
                id: entry.Id,
                pluralId: entry.PluralId,
                value: entry.Value,
                pluralValues: entry.PluralValues);
            return newEntry;

            bool IsAlreadyCompatible()
            {
                bool hitNonCommentOrWhitespace = false;
                foreach (GetTextLine line in entry.Lines)
                {
                    if (line.IsCommentOrWhiteSpace)
                    {
                        if (hitNonCommentOrWhitespace) return false;
                        continue;
                    }
                    hitNonCommentOrWhitespace = true;

                    //inline
                    if (line.Comment != null) return false;
                }

                return true;
            }
        }

        private class EntryComparer : IComparer<GetTextEntry>
        {
            public static EntryComparer Instance { get; } = new EntryComparer();
            public int Compare(GetTextEntry x, GetTextEntry y)
                => StringComparer.OrdinalIgnoreCase.Compare(
                    x.Header.References?.Count > 0 ? x.Header.References[0] : string.Empty,
                    y.Header.References?.Count > 0 ? y.Header.References[0] : string.Empty);
        }
    }
}
