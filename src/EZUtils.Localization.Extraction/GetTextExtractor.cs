namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Operations;

    public class GetTextExtractor
    {
        //in previous designs, we passed catalog root (effectively root of assembly cs files) along with code
        //technically, this allowed us to pass multiple assemblies worth of files
        //but using compilations implies only one assembly, so this design change is natural
        private readonly AssemblyPathResolver assemblyPathResolver;
        private readonly List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
        private readonly IGetTextExtractionWorkRunner extractionQueue;
        private CSharpCompilation compilation;

        public GetTextExtractor(
            AssemblyPathResolver assemblyPathResolver,
            Func<CSharpCompilation, CSharpCompilation> compilationBuilder,
            IGetTextExtractionWorkRunner extractionQueue)
        {
            this.assemblyPathResolver = assemblyPathResolver;
            CSharpCompilation compilation = CSharpCompilation.Create(assemblyPathResolver.AssemblyFullName)
                //we need a reference for each thing that we inspect from EZLocalization
                .AddReferences(MetadataReference.CreateFromFile(typeof(GetTextCatalog).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(GetTextExtractor).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(CultureInfo).Assembly.Location));

            this.compilation = compilationBuilder?.Invoke(compilation) ?? compilation;
            this.extractionQueue = extractionQueue;
        }

        public void AddFile(string sourceFilePath, string displayPath)
            => AddSource(source: File.ReadAllText(sourceFilePath), displayPath: displayPath);

        public void AddSource(string source, string displayPath)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, path: displayPath);
            compilation = compilation.AddSyntaxTrees(syntaxTree);
            syntaxTrees.Add(syntaxTree);
        }

        public void Extract(GetTextCatalogBuilder catalogBuilder)
        {
            foreach (SyntaxTree syntaxTree in syntaxTrees)
            {
                extractionQueue.StartWork(() =>
                {
                    SemanticModel model = compilation.GetSemanticModel(syntaxTree);
                    SyntaxWalker syntaxWalker = new SyntaxWalker(model, catalogBuilder, assemblyPathResolver);
                    syntaxWalker.VisitCompilationUnit(syntaxTree.GetCompilationUnitRoot());
                });
            }
        }

        private class SyntaxWalker : CSharpSyntaxWalker
        {
            private readonly SemanticModel model;
            private readonly GetTextCatalogBuilder catalogBuilder;
            private readonly AssemblyPathResolver assemblyPathResolver;

            public SyntaxWalker(SemanticModel model, GetTextCatalogBuilder catalogBuilder, AssemblyPathResolver assemblyPathResolver)
            {
                this.model = model;
                this.catalogBuilder = catalogBuilder;
                this.assemblyPathResolver = assemblyPathResolver;
            }

            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                ProcessDeclarationSymbol(model.GetDeclaredSymbol(node), out bool continueVisiting);
                if (continueVisiting)
                {
                    base.VisitClassDeclaration(node);
                }
            }

            public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                ProcessDeclarationSymbol(model.GetDeclaredSymbol(node), out bool continueVisiting);
                if (continueVisiting)
                {
                    base.VisitPropertyDeclaration(node);
                }
            }

            public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
            {
                ProcessDeclarationSymbol(model.GetDeclaredSymbol(node), out bool continueVisiting);
                if (continueVisiting)
                {
                    base.VisitVariableDeclarator(node);
                }
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                base.VisitInvocationExpression(node);

                //NOTE: this requires slightly more thought
                //if a compilation doesnt have the necessary references added, we won't be able to get an operation
                //this is hard to debug for those that might write custom extraction.
                //but there are many invocations that wont result in an operation that we don't care about,
                //so logging and whatnot isn't useful.
                if (!(model.GetOperation(node) is IInvocationOperation invocationOperation)) return;

                ImmutableArray<AttributeData> attributes = invocationOperation.TargetMethod.GetAttributes();
                if (!invocationOperation.TargetMethod
                    .GetAttributes()
                    .Any(a => a.AttributeClass.ToString() == "EZUtils.Localization.LocalizationMethodAttribute")) return;

                InvocationParser invocationParser = InvocationParser.ForInvocation(invocationOperation, assemblyPathResolver);
                if (!invocationParser.Success) return;

                foreach ((string poFilePath, Locale locale) in invocationParser.Targets)
                {
                    _ = catalogBuilder
                        //TODO: the current design has a hard dependency on file io, which isnt entirely ideal
                        //as it makes it hard to unit test. granted, extraction is supposed to write to files.
                        //we currently work around it by referring to a non-existent file so we dont accidentally load it
                        .ForPoFile(poFilePath, locale, changeLocaleIfDifferent: true, doc => doc
                            .AddEntry(e =>
                            {
                                FileLinePositionSpan location = node.GetLocation().GetLineSpan();
                                _ = e
                                    .AddEmptyLine() //entries tend to have whitespace on top to visually separate them
                                    .AddHeaderReference(location.Path, location.StartLinePosition.Line)
                                    .ConfigureContext(invocationParser.Context);
                                if (invocationParser.Id.OriginalFormat is string idFormat)
                                {
                                    _ = e.AddComment($" id format: {idFormat}");
                                }
                                _ = e.ConfigureId(invocationParser.Id.Value);

                                (bool countFound, ExtractedString extractedPluralId) = invocationParser.PluralStatus;
                                if (countFound && extractedPluralId.Value is string pluralId)
                                {
                                    if (extractedPluralId.OriginalFormat is string pluralFormat)
                                    {
                                        _ = e.AddComment($" plural id format: {pluralFormat}");
                                    }
                                    _ = e.ConfigureAsPlural(pluralId, locale);
                                }
                                else
                                {
                                    _ = e.ConfigureValue(string.Empty);
                                }
                            }));
                }
            }

            private void ProcessDeclarationSymbol(ISymbol symbol, out bool continueVisiting)
            {
                if (symbol == null)
                {
                    //we only stop visiting if we're sure; not if we dont know how to get a symbol
                    continueVisiting = true;
                    return;
                }

                IReadOnlyList<(string poFilePath, Locale locale)> targets =
                    GenerateLanguageAttributeParser.ParseTargets(symbol, symbol.GetAttributes(), assemblyPathResolver);
                if (targets.Count == 0)
                {
                    //we support GenerateLanguageAttribute on classes, fields, and properties
                    //for classes, we dont support localizing such classes, which themselves are meant to provide localization
                    //  NOTE: a slight improvement would be to additionally ensure the declared method isn't a LocalizationMethod
                    //  but since these types should be doing localization themselves, we'll save the time
                    //for fields and properties, we don't support them being localized, either.
                    continueVisiting = true;
                    return;
                }

                //even though we wont extract from these, we still want to add the files
                //even if there are no (valid) calls to this class/field/property, we want an entryless file
                foreach ((string poFilePath, Locale locale) in targets)
                {
                    _ = catalogBuilder.ForPoFile(poFilePath, locale, changeLocaleIfDifferent: true, _ => { });
                }

                continueVisiting = false;
            }
        }
    }
}
