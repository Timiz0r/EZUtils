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
            int existingEntryIndex = document.FindEntry(builtEntry.Context, builtEntry.Id, out GetTextEntry existingEntry);

            if (existingEntryIndex == -1)
            {
                updatedEntries.Add(builtEntry);
            }
            else
            {
                updatedEntries[existingEntryIndex] = MergeEntries(existingEntry: existingEntry, builtEntry: builtEntry);
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
        private static GetTextEntry MergeEntries(GetTextEntry existingEntry, GetTextEntry builtEntry)
        {
            string builtEntryReference = builtEntry.Header.References[0];
            bool existingReferenceFound = existingEntry.Header.References.Contains(builtEntryReference);
            if (!existingEntry.IsObsolete && existingReferenceFound)
            {
                return existingEntry;
            }

            GetTextEntryHeader header = existingEntry.Header;
            //avoid reallocation if adding a reference line
            List<GetTextLine> mergedLines = new List<GetTextLine>(existingEntry.Lines.Count + 1);
            mergedLines.AddRange(
                existingEntry.Lines.Select(l => l.IsMarkedObsolete
                    ? GetTextLine.Parse(l.Comment.Substring(1).TrimStart())
                    : l));

            if (!existingReferenceFound)
            {
                int lastReferenceLine = mergedLines.FindLastIndex(l => l.IsComment && l.Comment.StartsWith(":"));
                int referenceInsertionPoint = lastReferenceLine != -1
                    ? lastReferenceLine + 1
                    : mergedLines.FindIndex(l => l.Keyword != null);
                mergedLines.Insert(referenceInsertionPoint, new GetTextLine(comment: $": {builtEntryReference}"));

                header = new GetTextEntryHeader(
                    existingEntry.Header.References.Append(builtEntryReference).ToArray());
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
