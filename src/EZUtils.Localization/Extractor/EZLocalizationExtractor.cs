namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
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


            var extractor = new GetTextExtractor(compilation);
            void AddFile(string path) => extractor.AddFile(Path.GetFullPath(path), path);
            AddFile("Packages/com.timiz0r.ezutils.localization/ManualTestingEditorWindow.cs");
            AddFile("Packages/com.timiz0r.ezutils.localization/Florp.cs");
            AddFile("Packages/com.timiz0r.ezutils.localization/Localization.cs");
            extractor.Extract();
            //TODO:
            //original plan was to accumulate a large list of extracted entries and aggregate them together
            //screw that. let's add a series of types that allows us to load in existing catalogs as we find  them,
            //can add and merge entries in as we find them, and whatever else in a pretty declarative way.
            //TODO: 
        }
    }

    public class GetTextExtractor : CSharpSyntaxWalker
    {
        private readonly List<(string relativePath, SyntaxTree syntaxTree)> files =
            new List<(string relativePath, SyntaxTree syntaxTree)>();
        private CSharpCompilation compilation;

        private readonly List<ExtractionRecord> extractedFiles = new List<ExtractionRecord>();

        private List<GetTextEntry> currentEntries;
        private string currentFile;
        private SemanticModel currentModel;
        private string currentCatalogRoot;


        public GetTextExtractor(CSharpCompilation compilation)
        {
            this.compilation = compilation;
        }

        public void AddFile(string path, string displayPath)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));
            compilation = compilation.AddSyntaxTrees(syntaxTree);
            files.Add((displayPath, syntaxTree));
        }

        public void Extract()
        {
            foreach ((string relativePath, SyntaxTree syntaxTree) in files)
            {
                currentEntries = new List<GetTextEntry>();
                currentModel = compilation.GetSemanticModel(syntaxTree);
                currentFile = relativePath;

                VisitCompilationUnit(syntaxTree.GetCompilationUnitRoot());
            }
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);

            if (!(currentModel.GetOperation(node) is IInvocationOperation invocationOperation)) return;

            ImmutableArray<AttributeData> attributes = invocationOperation.TargetMethod.GetAttributes();
            if (!invocationOperation.TargetMethod
                .GetAttributes()
                .Any(a => a.AttributeClass.ToString() == "EZUtils.Localization.LocalizationMethodAttribute")) return;

            EntryExtractor entryExtractor = EntryExtractor.ForInvocation(invocationOperation, entryExtractor);
            foreach (IArgumentOperation argument in invocationOperation.Arguments)
            {
                entryExtractor.HandleArgument(argument);
            }

            
        }
    }

    public class EntryExtractor
    {
        private List<Locale> locales = null;
        private string catalogRoot = null;

        private string context = null;
        private string id = null;
        private bool isPlural = false;
        private string other = null;

        //so it turns out we dont need any plural form other than other
        //but already went through the effort of extracting this data
        //will leave it for now, even if unused, but it can be remove later if necessary
        private string zero = null;
        private string two = null;
        private string few = null;
        private string many = null;
        private string specialZero = null;

        private EntryExtractor() { }

        public static EntryExtractor ForInvocation(IInvocationOperation invocationOperation)
        {
            ImmutableArray<AttributeData> targetAttributes =
                invocationOperation.Instance is IMemberReferenceOperation memberReferenceOperation
                    ? memberReferenceOperation.Member.GetAttributes()
                    : invocationOperation.Instance == null
                        ? invocationOperation.TargetMethod.ContainingType.GetAttributes()
                        : throw new InvalidOperationException(
                            $"Unable to extract any attributes in order to find catalog generation attributes.");

            List<Locale> locales = targetAttributes
                .Where(a => a.AttributeClass.ToString() == "EZUtils.Localization.GenerateLanguageAttribute")
                .Select(a =>
                {
                    CultureInfo cultureInfo = CultureInfo.GetCultureInfo((string)a.ConstructorArguments[0].Value);

                    string GetRule(string ruleKind)
                        => (string)a.NamedArguments.SingleOrDefault(kvp => kvp.Key == "Zero").Value.Value;
                    PluralRules pluralRules = new PluralRules(
                        zero: GetRule("Zero"),
                        one: GetRule("One"),
                        two: GetRule("Two"),
                        few: GetRule("Few"),
                        many: GetRule("Many"),
                        other: GetRule("Other"));

                    bool useSpecialZero =
                        a.NamedArguments.SingleOrDefault(kvp => kvp.Key == "UseSpecialZero").Value.Value is bool b && b;

                    Locale locale = new Locale(cultureInfo, pluralRules, useSpecialZero);
                    return locale;
                }).ToList();

            string catalogRoot = (string)targetAttributes
                .SingleOrDefault(a => a.AttributeClass.ToString() == "EZUtils.Localization.GenerateCatalogAttribute")
                .ConstructorArguments[0].Value;

            EntryExtractor entryExtractor = new EntryExtractor
            {
                locales = locales,
                catalogRoot = catalogRoot
            };
            return entryExtractor;
        }

        public void HandleArgument(IArgumentOperation argument)
        {
            string parameterType = argument.Parameter.Type.ToString();
            if (parameterType != "string"
                && parameterType != "EZUtils.Localization.RawString"
                && parameterType != "System.FormattableString"
                && parameterType != "decimal") return;

            if (parameterType == "decimal" && argument.Parameter.Name == "count")
            {
                isPlural = true;
                return;
            }

            AttributeData localizationParameterAttribute = argument.Parameter.GetAttributes().SingleOrDefault(
                a => a.AttributeClass.ToString() == "EZUtils.Localization.LocalizationParameterAttribute");
            string parameterName = localizationParameterAttribute != null
                && localizationParameterAttribute.ConstructorArguments[0].Value is string p
                    ? p
                    : argument.Parameter.Name;

            string value = GetString(argument);
            switch (parameterName)
            {
                case LocalizationParameter.Context:
                    context = value;
                    break;
                case LocalizationParameter.Id:
                    id = value;
                    break;
                case LocalizationParameter.Other:
                    other = value;
                    break;
                case LocalizationParameter.Zero:
                    zero = value;
                    break;
                case LocalizationParameter.Two:
                    two = value;
                    break;
                case LocalizationParameter.Few:
                    few = value;
                    break;
                case LocalizationParameter.Many:
                    many = value;
                    break;
                case LocalizationParameter.SpecialZero:
                    specialZero = value;
                    break;
                default:
                    //we allow custom localization methods that have different parameters. for instance, EZLocalization's TranslateWindowTitle
                    //also we dont need to extract count; we just check that it's there as the main factor for determining
                    //if the method is for plurals or not
                    break;
            }
        }

        //NOTE: will want to rethrow in order to display invocation
        public ExtractionRecord Extract()
        {
            GetTextEntryBuilder builder = new GetTextEntryBuilder();

            _ = builder.ConfigureId(id ?? throw new InvalidOperationException("Unable to extract 'id'."));

            if (context != null) _ = builder.ConfigureContext(context);

            if (isPlural)
            {
                if (other == null) throw new InvalidOperationException("Unable to extract 'other'.");

                _ = builder.ConfigureAsPlural(other);

                if (plur)
            }

            return null;
        }

        private static string GetString(IOperation operation)
        {
            if (operation is IArgumentOperation argumentOperation)
            {
                return GetString(argumentOperation.Value);
            }

            if (operation is IConversionOperation conversionOperation)
            {
                return GetString(conversionOperation.Operand);
            }

            if (operation is ILiteralOperation literalOperation && literalOperation.Type.ToString() == "string")
            {
                return (string)literalOperation.ConstantValue.Value;
            }

            if (operation is IInterpolatedStringOperation interpolatedStringOperation)
            {
                StringBuilder interpolationStringBuilder = new StringBuilder();
                int formatIndex = 0;
                foreach (IInterpolatedStringContentOperation part in interpolatedStringOperation.Parts)
                {
                    if (part is IInterpolatedStringTextOperation textOperation)
                    {
                        string text = GetString(textOperation.Text);
                        _ = interpolationStringBuilder.Append(text);
                    }
                    else if (part is IInterpolationOperation interpolationOperation)
                    {
                        _ = interpolationStringBuilder.Append('{').Append(formatIndex++);
                        if (interpolationOperation.Alignment is ILiteralOperation alignment)
                        {
                            _ = interpolationStringBuilder.Append(',').Append(alignment.ConstantValue.ToString());
                        }
                        if (interpolationOperation.FormatString is ILiteralOperation format)
                        {
                            _ = interpolationStringBuilder.Append(':').Append(format.ConstantValue.ToString());
                        }
                        _ = interpolationStringBuilder.Append('}');
                    }
                    //we dont support IInterpolatedStringAppendOperation
                    //because we dont use InterpolatedStringHandlerAttribute in our apis
                    else throw new InvalidOperationException($"Extracting from '{part.Kind}' is not supported.");
                }
                return interpolationStringBuilder.ToString();
            }

            if (operation is IBinaryOperation binaryOperation && binaryOperation.OperatorKind == BinaryOperatorKind.Add)
            {
                return string.Concat(
                    GetString(binaryOperation.LeftOperand),
                    GetString(binaryOperation.RightOperand));
            }

            if (operation is IDefaultValueOperation)
            {
                return default;
            }

            if (operation is IFieldReferenceOperation fieldReferenceOperation)
            {
                if (!fieldReferenceOperation.ConstantValue.HasValue) throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    $"{fieldReferenceOperation.Type} is not a constant. Reference: {fieldReferenceOperation.Syntax}");

                return (string)fieldReferenceOperation.ConstantValue.Value;
            }

            throw new ArgumentOutOfRangeException(nameof(operation), $"Do not know how to handle extracting string from '{operation.Kind}'. Current operation: {operation.Syntax}");
        }
    }


    public class ExtractionRecord
    {
        public string FilePath { get; }
        public string CatalogRoot { get; }
        public IReadOnlyList<(Locale locale, GetTextEntry entry)> ExtractedEntries { get; }
    }


}
