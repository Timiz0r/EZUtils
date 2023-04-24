namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    public static class LocalizationProxyGenerator
    {
        public static void Generate(string assemblyRoot)
        {
            DirectoryInfo directory = new DirectoryInfo(assemblyRoot);
            foreach (SyntaxTree syntaxTree in directory
                    .EnumerateFiles()
                    .Select(f =>
                    {
                        string path = PathUtil.GetRelative(directory.FullName, f.FullName, newRoot: assemblyRoot);
                        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(f.FullName), path: path);
                        return syntaxTree;
                    }))
            {
                SyntaxNode root = syntaxTree.GetRoot();
                foreach (ClassDeclarationSyntax classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (!classDeclaration.AttributeLists
                        .SelectMany(al => al.Attributes)
                        .Any(a => a.Name.ToString() == "LocalizationProxy")) continue;

                    //are better ways of doing this, but, since we use EZLocalization.cs as the reference
                    //we can make mostly safe assumptions about the namespace. the only hitch is first-time generation,
                    //where the user could, for instance, nest namespaces.
                    string[] declaredNamespaces = root
                        .DescendantNodes()
                        .OfType<NamespaceDeclarationSyntax>()
                        .Select(ns => ns.Name.ToString())
                        .ToArray();
                    if (declaredNamespaces.Length != 1) throw new InvalidOperationException(
                        $"Cannot generate a localization proxy from '{syntaxTree.FilePath}' because there is not exactly one declared namespace.");

                    //we could consider checking to ensure there's only one class declaration in the file
                    //since we'll be overwriting everything else

                    SyntaxTree newSyntaxTree = ProxyRewriter.Generate(declaredNamespaces[0], classDeclaration);
                    File.WriteAllText(newSyntaxTree.FilePath, newSyntaxTree.ToString());
                }
            }
        }

        //NOTE: we're opting for a design where we dont allow modifications, else they be overwritten
        //this is the easiest way to implement this. if users want customization, they can create their own types
        //that call into this generated proxy type.
        private class ProxyRewriter : CSharpSyntaxRewriter
        {
            private static readonly Lazy<SyntaxTree> referenceSyntaxTree = new Lazy<SyntaxTree>(
                () => CSharpSyntaxTree.ParseText(
                    File.ReadAllText(
                        Path.GetFullPath("Packages/com.timiz0r.ezutils.localization/EZLocalization.cs"))),
                LazyThreadSafetyMode.None);
            private readonly string targetNamespace;
            private readonly ClassDeclarationSyntax originalClassDeclaration;
            private string fieldName = null;

            private ProxyRewriter(string targetNamespace, ClassDeclarationSyntax originalClassDeclaration)
            {
                this.targetNamespace = targetNamespace;
                this.originalClassDeclaration = originalClassDeclaration;
            }

            public static SyntaxTree Generate(string targetNamespace, ClassDeclarationSyntax originalClassDeclaration)
            {
                ProxyRewriter rewriter = new ProxyRewriter(targetNamespace, originalClassDeclaration);
                SyntaxNode newRoot = rewriter.Visit(referenceSyntaxTree.Value.GetRoot());
                SyntaxTree result = referenceSyntaxTree.Value
                    .WithRootAndOptions(newRoot, CSharpParseOptions.Default)
                    .WithFilePath(originalClassDeclaration.SyntaxTree.FilePath);
                return result;
            }

            public override SyntaxNode VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
            {
                NamespaceDeclarationSyntax newNode = node
                    .WithName(SyntaxFactory.ParseName(targetNamespace).WithTriviaFrom(node.Name))
                    .WithUsings(
                        SyntaxFactory.List(
                            new[]
                            {
                                Using("System"),
                                Using("System.Globalization"),
                                Using("EZUtils.Localization"),
                                Using("UnityEditor"),
                                Using("UnityEngine.UIElements")
                            }));
                return base.VisitNamespaceDeclaration(newNode);

                UsingDirectiveSyntax Using(string name) => SyntaxFactory.UsingDirective(
                    SyntaxFactory.ParseName(name)
                        .WithLeadingTrivia(SyntaxFactory.Space))
                    .WithTriviaFrom(node.Usings[0]);
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                string className = node.Identifier.ValueText;
                if (node.Identifier.ValueText != "EZLocalization") return null;

                FieldDeclarationSyntax field = originalClassDeclaration.Members
                    .OfType<FieldDeclarationSyntax>()
                    .FirstOrDefault(f => f.Declaration.Type.ToString() == "EZLocalization")
                        ?? (FieldDeclarationSyntax)SyntaxFactory
                        .ParseMemberDeclaration(
                            "private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder(\"TODO: path to catalog\", \"EZUtils\");")
                        .WithTriviaFrom(node.Members[0]);
                //no reason why this would be customized, but since we dont check the name of preexisting fields
                //we'll be extra safe
                fieldName = field.Declaration.Variables[0].Identifier.ValueText;

                ClassDeclarationSyntax newNode = node
                    .WithModifiers(originalClassDeclaration.Modifiers)
                    .WithAttributeLists(originalClassDeclaration.AttributeLists)
                    .WithIdentifier(originalClassDeclaration.Identifier)
                    .WithMembers(node.Members.Insert(0, field))
                    .WithTriviaFrom(node);
                return base.VisitClassDeclaration(newNode);
            }

            public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
            {
                //if not present, was generated in VisitClassDeclaration
                if (node.Declaration.Type.ToString() != "EZLocalization") return null;

                //we do let the user customize this, for path and sync key
                //we also have no need to visit further
                return node.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                if (!node.ChildTokens().Any(t => t.IsKind(SyntaxKind.PublicKeyword))
                    || node.ChildTokens().Any(t => t.IsKind(SyntaxKind.StaticKeyword))) return null;

                if (fieldName == null) throw new InvalidOperationException(
                    "Somehow did not find or generate an EZLocalization field.");

                ArgumentSyntax[] arguments = node.ParameterList.Parameters
                    .Select(p => SyntaxFactory.Argument(
                        SyntaxFactory.NameColon(
                            SyntaxFactory.IdentifierName(p.Identifier.ValueText),
                            SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space)),
                        default,
                        SyntaxFactory.IdentifierName(p.Identifier.ValueText)))
                    .ToArray();
                IEnumerable<SyntaxToken> separators = Enumerable.Repeat(
                    SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space),
                    arguments.Length - 1);
                ArgumentListSyntax argumentList = SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(arguments, separators));

                MethodDeclarationSyntax newNode = node
                    .WithModifiers(
                        SyntaxFactory.TokenList(
                            SyntaxFactory.Token(SyntaxKind.PublicKeyword).WithTriviaFrom(node.Modifiers[0]),
                            SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
                    .WithParameterList(node.ParameterList.WithTrailingTrivia(SyntaxFactory.Space))
                    .WithExpressionBody(
                        SyntaxFactory.ArrowExpressionClause(
                            SyntaxFactory.Token(SyntaxKind.EqualsGreaterThanToken).WithTrailingTrivia(SyntaxFactory.Space),
                            SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName(fieldName),
                                    SyntaxFactory.IdentifierName(node.Identifier.ValueText)),
                            argumentList)))
                    .WithTriviaFrom(node);
                //we have no need to visit further
                return newNode;
            }

            //these arent needed in the proxy type and shall be stripped
            public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node) => null;
            public override SyntaxNode VisitEventDeclaration(EventDeclarationSyntax node) => null;
            public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node) => null;
            public override SyntaxNode VisitIndexerDeclaration(IndexerDeclarationSyntax node) => null;
            public override SyntaxNode VisitDelegateDeclaration(DelegateDeclarationSyntax node) => null;
            public override SyntaxNode VisitOperatorDeclaration(OperatorDeclarationSyntax node) => null;
            public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node) => null;
            public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) => null;
            public override SyntaxNode VisitRecordDeclaration(RecordDeclarationSyntax node) => null;
            public override SyntaxNode VisitEventFieldDeclaration(EventFieldDeclarationSyntax node) => null;
            public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node) => null;
            public override SyntaxNode VisitDestructorDeclaration(DestructorDeclarationSyntax node) => null;
            public override SyntaxNode VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node) => null;
        }
    }
}
