namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using UnityEditor;

    //TODO: cr and refactor
    public class EZLocalizationExtractor
    {
        private readonly GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
        private readonly IGetTextExtractionWorkRunner extractionWorkRunner = GetTextExtractionWorkRunner.Create();
        private readonly List<(string path, string root)> uxmlPathsToExtract = new List<(string, string)>();

        public void ExtractFrom(AssemblyDefinition assemblyDefinition)
        {
            GetTextExtractor getTextExtractor = new GetTextExtractor(
                compilation => compilation
                    .AddReferences(MetadataReference.CreateFromFile(assemblyDefinition.Assembly.Location))
                    .AddReferences(MetadataReference.CreateFromFile(typeof(EditorWindow).Assembly.Location)),
                extractionWorkRunner);
            string assemblyRoot = assemblyDefinition.Root;
            DirectoryInfo assemblyRootDirectory = new DirectoryInfo(assemblyRoot);
            foreach (FileInfo file in assemblyDefinition.GetFiles("*.cs"))
            {
                string displayPath = PathUtil.GetRelative(
                    assemblyRootDirectory.FullName, file.FullName, newRoot: assemblyRoot);
                getTextExtractor.AddFile(
                    sourceFilePath: file.FullName,
                    displayPath: displayPath,
                    catalogRoot: assemblyRoot);
            }
            getTextExtractor.Extract(catalogBuilder);

            uxmlPathsToExtract.AddRange(
                assemblyDefinition.GetFiles("*.uxml").Select(f => (
                    path: PathUtil.GetRelative(assemblyRootDirectory.FullName, f.FullName, newRoot: assemblyRoot),
                    root: assemblyRoot)));
        }

        public void Finish()
        {
            extractionWorkRunner.FinishWork();

            UxmlExtractor uxmlExtractor = new UxmlExtractor(catalogBuilder);
            foreach ((string path, string root) in uxmlPathsToExtract)
            {
                uxmlExtractor.Extract(uxmlFilePath: path, root: root);
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

        //working under the assumption that more common entries are more interesting and should be higher up
        //a couple alternate implementations to consider
        //* first sort by directory of candidate reference, before sorting by reference count
        //* pick candidate reference not by first reference, but by finding the most common sub-directory, then picking the first reference
        private class EntryComparer : IComparer<GetTextEntry>
        {
            public static EntryComparer Instance { get; } = new EntryComparer();
            public int Compare(GetTextEntry x, GetTextEntry y)
            {
                bool xIsHeader = x.Context == null && x.Id.Length == 0;
                bool yIsHeader = y.Context == null && y.Id.Length == 0;
                int isHeaderComparer = Comparer<bool>.Default.Compare(yIsHeader, xIsHeader);
                if (isHeaderComparer != 0) return isHeaderComparer;

                int referenceCountComparison = y.Header.References.Count - x.Header.References.Count;
                if (referenceCountComparison != 0) return referenceCountComparison;

                (string path, int line) xReference = GetCandidateReference(x);
                (string path, int line) yReference = GetCandidateReference(x);
                int referencePathComparison = StringComparer.OrdinalIgnoreCase.Compare(xReference.path, yReference.path);
                if (referencePathComparison != 0) return referenceCountComparison;

                return yReference.line - xReference.line;

                (string path, int line) GetCandidateReference(GetTextEntry entry)
                    => entry.Header.References.Count == 0
                        ? default
                        : entry.Header.References[0] is string rawReference
                            && rawReference.Split(':') is string[] splitReference
                            && splitReference.Length == 2
                            && (
                                path: splitReference[0],
                                line: int.TryParse(splitReference[1], out int line) ? line : 0
                            ) is var result
                                ? result
                                : default;
            }
        }
    }
}
