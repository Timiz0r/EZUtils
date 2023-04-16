namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using JetBrains.Annotations;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Operations;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    //TODO: since we ended up going with roslyn, this gives us the opportunity to generate a proxy based on what EZLocalization looks like
    //we can generate it on domain reload and should be pretty deterministic. or we could play it safe and only write to the proxy if we see something isn't right.
    //it'll ideally be a two-pass thing. we'll generate the proxy, let domain reload happen, and extract (so that we can load up all the required syntax trees, including proxy)
    public class EZLocalizationExtractor
    {
        //TODO: ultimately want to do it per-assemblydefinition, so who knows what param we'll take
        //TODO: load up all the syntax trees for the assembly beforehand, which should allow us to do references from anywhere in the assembly
        //well, the idea there is to match up T calls to a catalog, but it's already hard anyway, since we cant easily find out, or may be impossible to find out, what the catalog is pointed to
        //TODO: so change of plans:
        //* have a GenerateCatalog("catalog path", "namespace", "ja", "kr" ,"etc") attribute that does langs and path
        //* the attribute can be added to fields, properties (so declarations), and classes (for static proxy types)
        public void ExtractFrom()
        {
            CSharpCompilation compilation = CSharpCompilation.Create("EZLocalizationExtractor")
                //we need a reference for each thing that we inspect from EZLocalization
                .AddReferences(MetadataReference.CreateFromFile(typeof(EZLocalization).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(CultureInfo).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(VisualElement).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(EditorWindow).Assembly.Location));


            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextExtractor extractor = new GetTextExtractor(compilation, catalogBuilder);
            void AddFile(string path) => extractor.AddFile(Path.GetFullPath(path), path);
            AddFile("Packages/com.timiz0r.ezutils.localization/ManualTestingEditorWindow.cs");
            AddFile("Packages/com.timiz0r.ezutils.localization/Florp.cs");
            AddFile("Packages/com.timiz0r.ezutils.localization/Localization.cs");
            var catalog = extractor.Extract();
        }
    }

    public class GetTextExtractor : CSharpSyntaxWalker
    {
        private readonly List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
        private readonly GetTextCatalogBuilder catalogBuilder;
        private CSharpCompilation compilation;

        //we cache it like this as we process each file in case it helps with perf
        //vaguely recall reading somewhere it does, but could be wrong
        private SemanticModel model;

        public GetTextExtractor(CSharpCompilation compilation, GetTextCatalogBuilder catalogBuilder)
        {
            this.compilation = compilation;
            this.catalogBuilder = catalogBuilder;
        }

        public void AddFile(string path, string displayPath)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: displayPath);
            compilation = compilation.AddSyntaxTrees(syntaxTree);
            syntaxTrees.Add(syntaxTree);
        }

        public GetTextCatalog Extract()
        {
            foreach (SyntaxTree syntaxTree in syntaxTrees)
            {
                model = compilation.GetSemanticModel(syntaxTree);
                VisitCompilationUnit(syntaxTree.GetCompilationUnitRoot());
            }

            return catalogBuilder.GetCatalog(Locale.English);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            INamedTypeSymbol symbol = model.GetDeclaredSymbol(node);
            //if a class has these attributes, it's a proxy type that calls into other LocalizationMethods
            //so we dont want to extract from such classes
            //NOTE: a slight improvement would be to additionally ensure the declared method isn't a LocalizationMethod
            //but since these types should be doing localization themselves, we'll save the time
            if (symbol.GetAttributes().Any(
                a => a.AttributeClass.ToString() == "EZUtils.Localization.GenerateLanguageAttribute")) return;

            base.VisitClassDeclaration(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);

            if (!(model.GetOperation(node) is IInvocationOperation invocationOperation)) return;

            ImmutableArray<AttributeData> attributes = invocationOperation.TargetMethod.GetAttributes();
            if (!invocationOperation.TargetMethod
                .GetAttributes()
                .Any(a => a.AttributeClass.ToString() == "EZUtils.Localization.LocalizationMethodAttribute")) return;

            InvocationParser invocationParser = InvocationParser.ForInvocation(invocationOperation);
            foreach (IArgumentOperation argument in invocationOperation.Arguments)
            {
                try
                {
                    invocationParser.HandleArgument(argument);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to parse invocation: {node}", ex);
                }
            }

            if (string.IsNullOrEmpty(invocationParser.Id)) throw new InvalidOperationException(
                $"Could not extract id from invocation: {node}");
            //should also theoretically invalidate an entry that has incomplete plural arguments

            foreach ((string poFilePath, Locale locale) in invocationParser.Targets)
            {
                string absolutePath = Path.GetFullPath(
                    Path.Combine(
                        Path.GetDirectoryName(node.SyntaxTree.FilePath),
                        poFilePath));
                _ = catalogBuilder
                    .ForPoFile(absolutePath, locale, doc => doc
                        .AddEntry(e =>
                        {
                            FileLinePositionSpan location = node.GetLocation().GetLineSpan();
                            _ = e.AddComment($": {location.Path}:{location.StartLinePosition.Line}");
                            _ = e.ConfigureContext(invocationParser.Context).ConfigureId(invocationParser.Id);

                            (bool countFound, string pluralId) = invocationParser.PluralStatus;
                            _ = countFound && pluralId != null
                                ? e.ConfigureAsPlural(pluralId, locale)
                                : e.ConfigureValue(string.Empty);
                        }));
            }
        }
    }

    public class GetTextCatalogBuilder
    {
        private readonly Dictionary<string, GetTextDocumentBuilder> documents =
            new Dictionary<string, GetTextDocumentBuilder>();

        public GetTextCatalogBuilder ForPoFile(
            string absolutePath, Locale locale, Action<GetTextDocumentBuilder> documentBuilderAction)
        {
            if (documents.TryGetValue(absolutePath, out GetTextDocumentBuilder document))
            {
                _ = document.VerifyLocaleMatches(locale);
                documentBuilderAction(document);

                return this;
            }

            document = documents[absolutePath] = GetTextDocumentBuilder.ForDocumentAt(absolutePath, locale);
            documentBuilderAction(document);

            return this;
        }

        public GetTextCatalogBuilder WriteToDisk()
        {
            return this;
        }

        public GetTextCatalog GetCatalog(Locale nativeLocale)
            => new GetTextCatalog(
                documents.Values.Select(db => db.GetGetTextDocument()).ToArray(),
                nativeLocale);
    }

    public class GetTextDocumentBuilder
    {
        private readonly string absolutePath;
        //hypothetically, we could instead store an IEnumerable of entries instead, avoiding a bunch of array allocations
        //or consider using immutable collections, even though library dependencies are annoying in unity
        private GetTextDocument document;

        private GetTextDocumentBuilder(string absolutePath)
        {
            this.absolutePath = absolutePath;
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

        public GetTextDocument GetGetTextDocument() => document;

        public GetTextDocumentBuilder WriteToDisk()
        {
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
                ? throw new InvalidOperationException($"Inconsistent locales for '{absolutePath}'.")
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
                mergedLines.Insert(lastReferenceLine + 1, new GetTextLine(comment: $": {builtEntryReference}"));

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
