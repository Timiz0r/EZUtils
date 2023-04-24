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
    //TODO: integration testing

    //from a ports-and-adapters-perspective, EZLocalization is an adapter; GetTextExtractor is a port
    //TODO: since we ended up going with roslyn, this gives us the opportunity to generate a proxy based on what EZLocalization looks like
    //we can generate it on domain reload and should be pretty deterministic. or we could play it safe and only write to the proxy if we see something isn't right.
    //it'll ideally be a two-pass thing. we'll generate the proxy, let domain reload happen, and extract (so that we can load up all the required syntax trees, including proxy)
    //  or maybe we can block domain reload (vaguely recall there's a way), generate, extract, and let it happen
    //actually, since we'll move extraction into a separate package, there too go the roslyn libs
    //instead, we'll just go template-style, or nvm, since the proxy doesnt depend on the generation code
    public class EZLocalizationExtractor
    {
        private readonly GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
        private readonly IGetTextExtractionWorkRunner extractionWorkRunner = GetTextExtractionWorkRunner.Create();
        private readonly List<string> uxmlPathsToExtract = new List<string>();

        public void ExtractFrom(string assemblyRoot)
        {
            GetTextExtractor getTextExtractor = new GetTextExtractor(
                compilation => compilation
                    .AddReferences(MetadataReference.CreateFromFile(typeof(EditorWindow).Assembly.Location)),
                extractionWorkRunner);
            DirectoryInfo directory = new DirectoryInfo(assemblyRoot);
            foreach (FileInfo file in directory.EnumerateFiles("*.cs", SearchOption.AllDirectories))
            {
                string displayPath = PathUtil.GetRelative(directory.FullName, file.FullName, newRoot: assemblyRoot);
                getTextExtractor.AddFile(
                    sourceFilePath: file.FullName,
                    displayPath: displayPath,
                    catalogRoot: assemblyRoot);
            }
            getTextExtractor.Extract(catalogBuilder);

            uxmlPathsToExtract.Add(assemblyRoot);
        }

        public void Finish()
        {
            extractionWorkRunner.FinishWork();

            UxmlExtractor uxmlExtractor = new UxmlExtractor(catalogBuilder);
            foreach (string path in uxmlPathsToExtract)
            {
                uxmlExtractor.ExtractAll(path);
            }

            _ = catalogBuilder
                .ForEachDocument(d => d
                    .Prune()
                    .ForEachEntry(e => GetCompatibilityVersion(e))
                    .SortEntries(EntryComparer.Instance))
                .WriteToDisk(root: string.Empty); //extractors end up using rooted paths
        }

        //TODO: eventually, if we have an editor that can support our more forgiving handling, we want the extraction
        //merge to refill in automated mid-entry comments. at time of writing, this is just plural rule comments
        public static GetTextEntry GetCompatibilityVersion(GetTextEntry entry)
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
