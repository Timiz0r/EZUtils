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
        //we dont store the doc, since we can be more efficient by storing underlying entries
        //but if things get too complicated, we can go back to storing the doc
        private ImmutableList<GetTextEntry> underlyingEntries = ImmutableList<GetTextEntry>.Empty;
        private ImmutableHashSet<(string context, string id, string pluralId)> encounteredEntries =
            ImmutableHashSet<(string context, string id, string pluralId)>.Empty;

        public string Path { get; }

        private GetTextDocumentBuilder(string path)
        {
            Path = path;
        }

        public static GetTextDocumentBuilder ForDocumentAt(string path, Locale locale)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (locale == null) throw new ArgumentNullException(nameof(locale));

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
            entryBuilderAction?.Invoke(builder);

            GetTextEntry builtEntry = builder.Create();
            bool firstEncounterOfEntry = ImmutableInterlocked.Update(
                ref encounteredEntries,
                (hs, e) => hs.Add(e), (context: builtEntry.Context, id: builtEntry.Id, pluralId: builtEntry.PluralId));

            _ = ImmutableInterlocked.Update(
                ref underlyingEntries,
                (oldEntries, b) =>
                    oldEntries.FindIndex(
                        e => e.Context == b.Context && e.Id == b.Id && e.PluralId == b.PluralId) is int existingIndex
                        && existingIndex == -1
                            ? oldEntries.Add(b)
                            : oldEntries.SetItem(
                                existingIndex,
                                MergeEntries(
                                    existingEntry: oldEntries[existingIndex],
                                    builtEntry: b,
                                    firstEncounterOfEntry: firstEncounterOfEntry)),
                builtEntry);

            return this;
        }

        public GetTextDocumentBuilder OverwriteEntry(Action<GetTextEntryBuilder> entryBuilderAction)
        {
            GetTextEntryBuilder builder = new GetTextEntryBuilder();
            entryBuilderAction?.Invoke(builder);

            GetTextEntry builtEntry = builder.Create();
            bool foundFirstInstanceOfEntry = ImmutableInterlocked.Update(
                ref encounteredEntries,
                (hs, e) => hs.Add(e), (context: builtEntry.Context, id: builtEntry.Id, pluralId: builtEntry.PluralId));

            _ = ImmutableInterlocked.Update(ref underlyingEntries, (oldEntries, b) =>
            {
                int existingIndex = oldEntries.FindIndex(e => e.Context == b.Context && e.Id == b.Id && e.PluralId == b.PluralId);
                if (existingIndex == -1) throw new InvalidOperationException(
                    $"Attempting to overwrite an entry, but it has not been added yet. Context: '{b.Context}', Id: '{b.Id}', PluralId: '{b.PluralId}'.");

                GetTextEntry newEntry = OverwriteEntry(oldEntries[existingIndex], b);
                return oldEntries.SetItem(existingIndex, newEntry);
            }, builtEntry);

            return this;
        }

        public GetTextDocumentBuilder SortEntries(IComparer<GetTextEntry> comparer)
        {
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            _ = ImmutableInterlocked.Update(ref underlyingEntries, entries => entries.Sort(comparer));
            return this;
        }

        //note that, in order to maintain thread safety, we may enumerate multiple times
        public GetTextDocumentBuilder ForEachEntry(Func<GetTextEntry, GetTextEntry> entryFunc)
        {
            _ = ImmutableInterlocked.Update(ref underlyingEntries, oldEntries =>
            {
                ImmutableList<GetTextEntry>.Builder builder = oldEntries.ToBuilder();
                for (int i = 0; i < builder.Count; i++)
                {
                    GetTextEntry oldEntry = builder[i];
                    GetTextEntry newEntry = entryFunc?.Invoke(oldEntry) ?? oldEntry;
                    if (oldEntry == newEntry) continue;
                    builder[i] = newEntry;
                }

                ImmutableList<GetTextEntry> newEntries = builder.ToImmutable();
                return newEntries;
            });

            return this;
        }

        public GetTextDocumentBuilder Prune()
        {
            return ForEachEntry(e =>
                (e.Context == null && e.Id.Length == 0) //header entry that wont have references
                    || e.Header.Flags.Contains("keep")
                    || encounteredEntries.Contains((e.Context, e.Id, e.PluralId))
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

#pragma warning disable CA1024 //Use properties where appropriate; not a natural property
        public GetTextDocument GetGetTextDocument() => new GetTextDocument(underlyingEntries);
#pragma warning restore CA1024

        public GetTextDocumentBuilder WriteToDisk(string root = "")
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
            if (locale == null) throw new ArgumentNullException(nameof(locale));

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
            _ = ImmutableInterlocked.Update(ref underlyingEntries,
                (oldEntries, l) => oldEntries.SetItem(
                    0,
                    new GetTextHeader(oldEntries[0])
                        .WithLocale(l)
                        .UnderlyingEntry),
                locale);

            return this;
        }

        //TODO: hit a minor issue where we generated more comments, but these comments dont end up in existing entries
        //plus, the implementation here is already pretty complicated. if possible, would be nice to have a nice
        //more generic merge logic that doesnt lose data. still, the current implementation is okay, since we maintain
        //existing data just fine, and comments arent all that important.
        private static GetTextEntry MergeEntries(GetTextEntry existingEntry, GetTextEntry builtEntry, bool firstEncounterOfEntry)
        {
            //avoid reallocation if adding a reference line
            List<GetTextLine> mergedLines = new List<GetTextLine>(existingEntry.Lines.Count + 1);
            //we mark lines non-obsolete of necessary because there being a merge inherently means the entry cannot be
            //obsolete by means of us finding it
            mergedLines.AddRange(
                existingEntry.Lines.Select(l => l.IsMarkedObsolete
                    ? GetTextLine.Parse(l.Comment.Substring(1).TrimStart()) //1 being the ~ char
                    : l));

            //NOTE: the current design only does reference merging, which makes this condition valid
            //but adding other kinds of merging would necessitate changes here
            if (builtEntry.Header.References.Count == 0)
            {
                GetTextEntry unobsoletedResult = GetTextEntry.Parse(mergedLines);
                return unobsoletedResult;
            }

            string builtEntryReference = builtEntry.Header.References[0];

            GetTextEntryHeader header = existingEntry.Header;
            //we go about pruning old references by wiping out the original set the first time we try
            //to merge with the original entry
            List<string> references = new List<string>(firstEncounterOfEntry ? 1 : header.References.Count + 1);
            if (!firstEncounterOfEntry)
            {
                references.AddRange(header.References);
            }

            bool existingReferenceFound = references.Contains(builtEntryReference);
            if (!existingEntry.IsObsolete && existingReferenceFound)
            {
                return existingEntry;
            }

            //we calculate the insertion point here because, after clearing the references (for pruning), we want to put
            //them back in the same spot they once were. granted, here, we no longer support non-contiguous reference comments,
            //but that's probably fine.
            int lastReferenceLine = mergedLines.FindLastIndex(
                l => l.IsComment && l.Comment.StartsWith(":", StringComparison.Ordinal));
            int referenceInsertionPoint = lastReferenceLine != -1
                ? lastReferenceLine + 1
                : mergedLines.FindIndex(l => l.Keyword != null);
            int removedLines = mergedLines.RemoveAll(
                l => firstEncounterOfEntry && l.IsComment && l.Comment.StartsWith(":", StringComparison.Ordinal));
            referenceInsertionPoint -= removedLines;

            if (!existingReferenceFound)
            {
                mergedLines.Insert(referenceInsertionPoint, new GetTextLine(comment: $": {builtEntryReference}"));

                header = new GetTextEntryHeader(
                    references.Append(builtEntryReference).ToArray(),
                    existingEntry.Header.Flags);
            }

            //previously, we just passed in merged set of line, since the value is otherwise expected to be the same
            //but we now parse to make sure we at least have a valid entry. could consider verifying they're the same,
            //but we theoretically have unit tests for that.
            GetTextEntry mergedResult = GetTextEntry.Parse(mergedLines);
            return mergedResult;
        }
        private static GetTextEntry OverwriteEntry(GetTextEntry existingEntry, GetTextEntry builtEntry)
        {
            List<GetTextLine> mergedLines = new List<GetTextLine>(existingEntry.Lines.Count);
            //we mark lines non-obsolete of necessary because there being a merge inherently means the entry cannot be
            //obsolete by means of us finding it
            mergedLines.AddRange(
                existingEntry.Lines.Select(l => l.IsMarkedObsolete
                    ? GetTextLine.Parse(l.Comment.Substring(1).TrimStart()) //1 being the ~ char
                    : l));

            //entry builders always produce valid entries, so id-related props arent in need of rewrite -- just values
            if (builtEntry.Value != null)
            {
                ReplaceLine(l => l.Keyword?.Keyword == "msgstr" && l.Keyword.Index == null);
            }
            for (int i = 0; i < builtEntry.PluralValues.Count; i++)
            {
                ReplaceLine(l => l.Keyword?.Keyword == "msgstr" && l.Keyword.Index == i);
            }

            GetTextEntry overwrittenResult = GetTextEntry.Parse(mergedLines);
            return overwrittenResult;

            void ReplaceLine(Func<GetTextLine, bool> predicate)
            {
                int oldIndex = mergedLines.FindIndex(l => predicate(l));
                GetTextLine newLine = builtEntry.Lines.Single(l => predicate(l));

                mergedLines[oldIndex] = newLine;
            }
        }
    }
}
