namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using UnityEditor;

    //TODO: integration test survival of invalid po file
    //  presumably T will end up throwing due to poor parsing. dont want that to happen. since this would be in unityland via catalogreference, we can log it!
    //  also test how reloads will work. also want to nullify and log. or keep current doc?
    internal class EZLocalizationExtractor
    {
        private readonly IAssemblyRootResolver assemblyRootResolver;
        private readonly GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
        private readonly IGetTextExtractionWorkRunner extractionWorkRunner = GetTextExtractionWorkRunner.CreateSynchronous();
        private readonly List<(string path, string root)> uxmlPathsToExtract = new List<(string, string)>();

        public EZLocalizationExtractor(IAssemblyRootResolver assemblyRootResolver)
        {
            this.assemblyRootResolver = assemblyRootResolver;
        }

        public void ExtractFrom(AssemblyDefinition assemblyDefinition)
        {
            string assemblyRoot = assemblyDefinition.Root;
            GetTextExtractor getTextExtractor = new GetTextExtractor(
                new AssemblyPathResolver(assemblyDefinition.Assembly.FullName, assemblyRoot, assemblyRootResolver),
                compilation => compilation
                    .AddReferences(MetadataReference.CreateFromFile(assemblyDefinition.Assembly.Location))
                    .AddReferences(assemblyDefinition.References.Select(a => MetadataReference.CreateFromFile(a.Location)))
                    .AddReferences(MetadataReference.CreateFromFile(typeof(EditorWindow).Assembly.Location)), //for window title translation
                extractionWorkRunner);
            foreach (FileInfo file in assemblyDefinition.GetFiles("*.cs"))
            {
                getTextExtractor.AddFile(
                    sourceFilePath: file.FullName,
                    displayPath: assemblyDefinition.GetUnityPath(file.FullName));
            }
            getTextExtractor.Extract(catalogBuilder);

            uxmlPathsToExtract.AddRange(
                assemblyDefinition.GetFiles("*.uxml").Select(f => (
                    path: assemblyDefinition.GetUnityPath(f.FullName),
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

            //a previous design just provided new set of lines and left other properties intact
            //but decided to do parsing to increase verification that we didn't break anything
            GetTextEntry newEntry = GetTextEntry.Parse(newLines);
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
                (string path, int line) yReference = GetCandidateReference(y);

                int referencePathComparison = StringComparer.OrdinalIgnoreCase.Compare(xReference.path, yReference.path);
                return referencePathComparison != 0
                    ? referencePathComparison
                    : xReference.line - yReference.line;

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
