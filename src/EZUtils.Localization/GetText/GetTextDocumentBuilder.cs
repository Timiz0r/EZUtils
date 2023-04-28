namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.CodeAnalysis;

    public class GetTextDocumentBuilder
    {
        //we dont store the doc, since we can be more efficient by storing underlying entries
        //but if things get too complicated, we can go back to storing the doc
        private ImmutableList<GetTextEntry> underlyingEntries = ImmutableList<GetTextEntry>.Empty;
        private ImmutableHashSet<(string context, string id, string pluralId)> foundEntries =
            ImmutableHashSet<(string context, string id, string pluralId)>.Empty;

        public string Path { get; }

        private GetTextDocumentBuilder(string path)
        {
            Path = path;
        }

        public static GetTextDocumentBuilder ForDocumentAt(string path, Locale locale)
        {
            GetTextDocumentBuilder builder = new GetTextDocumentBuilder(path)
            {
                underlyingEntries = File.Exists(path)
                    ? GetTextDocument.LoadFrom(path).Entries.ToImmutableList()
                    : ImmutableList<GetTextEntry>.Empty.Add(GetTextHeader.ForLocale(locale).UnderlyingEntry)
            };
            return builder;
        }

        public GetTextDocumentBuilder AddEntry(Action<GetTextEntryBuilder> entryBuilderAction)
        {
            GetTextEntryBuilder builder = new GetTextEntryBuilder();
            entryBuilderAction(builder);

            GetTextEntry builtEntry = builder.Create();
            bool foundFirstInstanceOfEntry = ImmutableInterlocked.Update(
                ref foundEntries,
                (hs, e) => hs.Add(e), (context: builtEntry.Context, id: builtEntry.Id, pluralId: builtEntry.PluralId));
            ImmutableList<GetTextEntry> entriesBeingProcessed = underlyingEntries;
            int existingEntryIndex = entriesBeingProcessed.FindIndex(
                e => e.Context == builtEntry.Context && e.Id == builtEntry.Id && e.PluralId == builtEntry.PluralId);

            if (existingEntryIndex == -1)
            {
                entriesBeingProcessed = entriesBeingProcessed.Add(builtEntry);
            }
            else
            {
                entriesBeingProcessed = entriesBeingProcessed.SetItem(existingEntryIndex, MergeEntries(
                    existingEntry: entriesBeingProcessed[existingEntryIndex],
                    builtEntry: builtEntry,
                    foundFirstInstanceOfEntry: foundFirstInstanceOfEntry));
            }

            _ = ImmutableInterlocked.Update(ref underlyingEntries, _ => entriesBeingProcessed);

            return this;
        }

        public GetTextDocumentBuilder SortEntries(IComparer<GetTextEntry> comparer)
        {
            _ = ImmutableInterlocked.Update(ref underlyingEntries, entries => entries.Sort(comparer));

            return this;
        }

        public GetTextDocumentBuilder ForEachEntry(Func<GetTextEntry, GetTextEntry> entryFunc)
        {
            ImmutableList<GetTextEntry>.Builder builder = underlyingEntries.ToBuilder();
            for (int i = 0; i < builder.Count; i++)
            {
                GetTextEntry oldEntry = builder[i];
                GetTextEntry newEntry = entryFunc(oldEntry);
                if (oldEntry == newEntry) continue;
                builder[i] = newEntry;
            }

            ImmutableList<GetTextEntry> newEntries = builder.ToImmutable();
            _ = ImmutableInterlocked.Update(ref underlyingEntries, _ => newEntries);

            return this;
        }

        public GetTextDocumentBuilder Prune()
        {
            return ForEachEntry(e =>
                (e.Context == null && e.Id.Length == 0) //header entry that wont have references
                    || e.Header.Flags.Contains("keep")
                    || foundEntries.Contains((e.Context, e.Id, e.PluralId))
                    //we no longer prune based on this because our code as now written cannot fully strip all references
                    //the first time we hit an entry, we do clear it, but then we also add it it
                    //the only way this happens is if a user does it, and we'll assume they did it for a good reason
                    // || e.Header.References.Count > 0
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

        public GetTextDocument GetGetTextDocument() => new GetTextDocument(underlyingEntries);

        public GetTextDocumentBuilder WriteToDisk(string root)
        {
            string savePath = Path;
            if (!System.IO.Path.IsPathRooted(savePath))
            {
                savePath = System.IO.Path.Combine(root, savePath);
            }
            //granted, even after adding the root, the path may still not be rooted
            //the check is just because adding a root to a rooted path makes no sense

            GetTextDocument document = GetGetTextDocument();
            document.Save(savePath);

            return this;
        }

        public GetTextDocumentBuilder VerifyLocaleMatches(Locale locale)
        {
            //two locales are equal if just their cultures are the same
            //but we want to verify that the author has consistent plural rules across potentially multiple declarations
            //of the same catalog
            Locale existingLocale = new GetTextHeader(underlyingEntries[0]).Locale;
            return existingLocale != locale
                || !existingLocale.PluralRules.Equals(locale.PluralRules)
                ? throw new InvalidOperationException($"Inconsistent locales for '{Path}'.")
                : this;
        }

        public GetTextDocumentBuilder SetLocale(Locale locale)
        {
            underlyingEntries = underlyingEntries.SetItem(0,
                new GetTextHeader(underlyingEntries[0])
                    .WithLocale(locale)
                    .UnderlyingEntry);

            return this;
        }

        //TODO: hit a minor issue where we generated more comments, but these comments dont end up in existing entries
        //plus, the implementation here is already pretty complicated. if possible, would be nice to have a nice
        //more generic merge logic that doesnt lose data. still, the current implementation is okay, since we maintain
        //existing data just fine, and comments arent all that important.
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
