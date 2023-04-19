namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using Microsoft.CodeAnalysis;

    public class GetTextDocumentBuilder
    {
        private readonly string path;
        //hypothetically, we could instead store an IEnumerable of entries instead, avoiding a bunch of array allocations
        //or consider using immutable collections, even though library dependencies are annoying in unity
        private GetTextDocument document;
        private readonly HashSet<(string context, string id)> foundEntries = new HashSet<(string context, string id)>();

        private GetTextDocumentBuilder(string path)
        {
            this.path = path;
        }

        public static GetTextDocumentBuilder ForDocumentAt(string absolutePath, Locale locale)
        {
            GetTextDocumentBuilder builder = new GetTextDocumentBuilder(absolutePath)
            {
                document = File.Exists(absolutePath)
                    ? GetTextDocument.LoadFrom(absolutePath)
                    : new GetTextDocument(new[] { new GetTextHeader(locale).ToEntry() })
            };
            return builder;
        }

        public GetTextDocumentBuilder AddEntry(Action<GetTextEntryBuilder> entryBuilderAction)
        {
            GetTextEntryBuilder builder = new GetTextEntryBuilder();
            entryBuilderAction(builder);

            //avoids an array reallocation in the event we add an item
            List<GetTextEntry> updatedEntries = new List<GetTextEntry>(document.Entries.Count + 1);
            updatedEntries.AddRange(document.Entries);

            GetTextEntry builtEntry = builder.Create();
            bool foundFirstInstanceOfEntry = foundEntries.Add((context: builtEntry.Context, id: builtEntry.Id));
            int existingEntryIndex = document.FindEntry(builtEntry.Context, builtEntry.Id, out GetTextEntry existingEntry);

            if (existingEntryIndex == -1)
            {
                updatedEntries.Add(builtEntry);
            }
            else
            {
                updatedEntries[existingEntryIndex] = MergeEntries(
                    existingEntry: existingEntry,
                    builtEntry: builtEntry,
                    foundFirstInstanceOfEntry: foundFirstInstanceOfEntry);
            }

            document = new GetTextDocument(updatedEntries);

            return this;
        }

        public GetTextDocumentBuilder SortEntries(IComparer<GetTextEntry> comparer)
        {
            GetTextEntry[] sortedEntries = document.Entries.OrderBy(e => e, comparer).ToArray();
            document = new GetTextDocument(sortedEntries);

            return this;
        }

        public GetTextDocumentBuilder ForEachEntry(Func<GetTextEntry, GetTextEntry> entryFunc)
        {
            GetTextEntry[] newEntries = document.Entries.Select(e => entryFunc(e)).ToArray();
            document = new GetTextDocument(newEntries);

            return this;
        }

        public GetTextDocumentBuilder Prune()
        {
            return ForEachEntry(e =>
                (e.Context == null && e.Id.Length == 0) //header entry that wont have references
                    || e.Header.Flags.Contains("keep")
                    || e.Header.References.Count > 0
                    ? e
                    : MarkObsolete(e));

            GetTextEntry MarkObsolete(GetTextEntry entry)
            {
                GetTextLine[] newLines = entry.Lines
                    .Select(l => l.StringValue != null || l.Keyword != null
                        ? new GetTextLine(comment: string.Concat("~ ", l.RawLine))
                        : l)
                    .ToArray();
                GetTextEntry newEntry = new GetTextEntry(
                    lines: newLines,
                    header: entry.Header,
                    isObsolete: true,
                    context: entry.Context,
                    id: entry.Id,
                    pluralId: entry.PluralId,
                    value: entry.Value,
                    pluralValues: entry.PluralValues);
                return newEntry;
            }
        }

        public GetTextDocument GetGetTextDocument() => document;

        public GetTextDocumentBuilder WriteToDisk(string root)
        {
            string savePath = path;
            if (!Path.IsPathRooted(savePath))
            {
                savePath = Path.Combine(root, savePath);
            }

            document.Save(savePath);

            return this;
        }

        public GetTextDocumentBuilder VerifyLocaleMatches(Locale locale)
        {
            //two locales are equal if just their cultures are the same
            //but we want to verify that the author has consistent plural rules across potentially multiple declarations
            //of the same catalog
            Locale existingLocale = document.Header.Locale;
            return existingLocale != locale
                || !existingLocale.PluralRules.Equals(locale.PluralRules)
                ? throw new InvalidOperationException($"Inconsistent locales for '{path}'.")
                : this;
        }
        private static GetTextEntry MergeEntries(GetTextEntry existingEntry, GetTextEntry builtEntry, bool foundFirstInstanceOfEntry)
        {
            string builtEntryReference = builtEntry.Header.References[0];

            GetTextEntryHeader header = existingEntry.Header;
            //we go about pruning old references by wiping out the original set the first time we try
            //to merge with the original entry
            List<string> references = new List<string>(foundFirstInstanceOfEntry ? 1 : header.References.Count + 1);
            if (!foundFirstInstanceOfEntry)
            {
                references.AddRange(header.References);
            }

            bool existingReferenceFound = references.Contains(builtEntryReference);
            if (!existingEntry.IsObsolete && existingReferenceFound)
            {
                return existingEntry;
            }

            //avoid reallocation if adding a reference line
            List<GetTextLine> mergedLines = new List<GetTextLine>(existingEntry.Lines.Count + 1);
            mergedLines.AddRange(existingEntry.Lines
                .Select(l => l.IsMarkedObsolete
                    ? GetTextLine.Parse(l.Comment.Substring(1).TrimStart()) //1 being the ~ char
                    : l));
            //we calculate the insertion point here because, after clearing the references (for pruning), we want to put
            //them back in the same spot they once were. granted, here, we no longer support non-contiguous reference comments,
            //but that's probably fine.
            int lastReferenceLine = mergedLines.FindLastIndex(
                l => l.IsComment && l.Comment.StartsWith(":", StringComparison.Ordinal));
            int referenceInsertionPoint = lastReferenceLine != -1
                ? lastReferenceLine + 1
                : mergedLines.FindIndex(l => l.Keyword != null);
            int removedLines = mergedLines.RemoveAll(
                l => foundFirstInstanceOfEntry && l.IsComment && l.Comment.StartsWith(":", StringComparison.Ordinal));
            referenceInsertionPoint -= removedLines;

            if (!existingReferenceFound)
            {
                mergedLines.Insert(referenceInsertionPoint, new GetTextLine(comment: $": {builtEntryReference}"));

                header = new GetTextEntryHeader(
                    references.Append(builtEntryReference).ToArray(),
                    existingEntry.Header.Flags);
            }

            GetTextEntry result = new GetTextEntry(
                mergedLines,
                header,
                isObsolete: false,
                context: existingEntry.Context,
                id: existingEntry.Id,
                pluralId: existingEntry.PluralId,
                value: existingEntry.Value,
                pluralValues: existingEntry.PluralValues);
            return result;
        }
    }
}
