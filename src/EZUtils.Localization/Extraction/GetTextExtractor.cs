namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Operations;

    public class GetTextExtractor
    {
        private readonly List<(SyntaxTree syntaxTree, string catalogRoot)> syntaxTrees = new List<(SyntaxTree, string)>();
        private readonly ActionBlock<Action> processorQueue;
        private CSharpCompilation compilation;

        public GetTextExtractor(
            Func<CSharpCompilation, CSharpCompilation> compilationBuilder,
            ActionBlock<Action> processorQueue)
        {
            CSharpCompilation compilation = CSharpCompilation.Create("EZLocalizationExtractor")
                //we need a reference for each thing that we inspect from EZLocalization
                .AddReferences(MetadataReference.CreateFromFile(typeof(GetTextExtractor).Assembly.Location))
                .AddReferences(MetadataReference.CreateFromFile(typeof(CultureInfo).Assembly.Location));

            this.compilation = compilationBuilder(compilation);
            this.processorQueue = processorQueue;
        }

        public void AddFile(string sourceFilePath, string displayPath, string catalogRoot)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFilePath), path: displayPath);
            compilation = compilation.AddSyntaxTrees(syntaxTree);
            syntaxTrees.Add((syntaxTree, catalogRoot));
        }

        public void Extract(GetTextCatalogBuilder catalogBuilder)
        {
            foreach ((SyntaxTree syntaxTree, string catalogRoot) in syntaxTrees)
            {
                _ = processorQueue.Post(() =>
                {
                    SemanticModel model = compilation.GetSemanticModel(syntaxTree);
                    SyntaxWalker syntaxWalker = new SyntaxWalker(model, catalogBuilder, catalogRoot);
                    syntaxWalker.VisitCompilationUnit(syntaxTree.GetCompilationUnitRoot());
                });
            }
        }

        private class SyntaxWalker : CSharpSyntaxWalker
        {
            private readonly SemanticModel model;
            private readonly GetTextCatalogBuilder catalogBuilder;
            private readonly string catalogRoot;

            public SyntaxWalker(SemanticModel model, GetTextCatalogBuilder catalogBuilder, string catalogRoot)
            {
                this.model = model;
                this.catalogBuilder = catalogBuilder;
                this.catalogRoot = catalogRoot;
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
                if (!invocationParser.Success) return;

                if (string.IsNullOrEmpty(invocationParser.Id)) throw new InvalidOperationException(
                    $"Could not extract id from invocation: {node}");
                //should also theoretically invalidate an entry that has incomplete plural arguments

                foreach ((string poFilePath, Locale locale) in invocationParser.Targets)
                {
                    string path = Path.Combine(catalogRoot, poFilePath);
                    _ = catalogBuilder
                        .ForPoFile(path, locale, doc => doc
                            .AddEntry(e =>
                            {
                                FileLinePositionSpan location = node.GetLocation().GetLineSpan();
                                _ = e
                                    .AddEmptyLine() //entries tend to have whitespace on top to visually separate them
                                    //TODO: add a header instead
                                    .AddComment($": {location.Path}:{location.StartLinePosition.Line}")
                                    .ConfigureContext(invocationParser.Context)
                                    .ConfigureId(invocationParser.Id);

                                (bool countFound, string pluralId) = invocationParser.PluralStatus;
                                _ = countFound && pluralId != null
                                    ? e.ConfigureAsPlural(pluralId, locale)
                                    : e.ConfigureValue(string.Empty);
                            }));
                }
            }
        }
    }
}
