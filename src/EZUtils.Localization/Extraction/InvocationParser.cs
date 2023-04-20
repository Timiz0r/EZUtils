namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Operations;

    public class InvocationParser
    {
        private readonly List<(string poFilePath, Locale locale)> targets;
        public IReadOnlyList<(string poFilePath, Locale locale)> Targets => targets;

        public string Context { get; private set; }
        public string Id { get; private set; }

        public (bool countFound, string pluralId) PluralStatus { get; private set; }

        private InvocationParser(List<(string poFilePath, Locale locale)> targets)
        {
            this.targets = targets;
        }

        public static InvocationParser ForInvocation(IInvocationOperation invocationOperation)
        {
            ImmutableArray<AttributeData> targetAttributes =
                invocationOperation.Instance is IMemberReferenceOperation memberReferenceOperation
                    ? memberReferenceOperation.Member.GetAttributes()
                    : invocationOperation.Instance == null
                        ? invocationOperation.TargetMethod.ContainingType.GetAttributes()
                        : throw new InvalidOperationException(
                            $"Unable to extract any attributes in order to find catalog generation attributes.");

            List<(string poFilePath, Locale locale)> targets = targetAttributes
                .Where(a => a.AttributeClass.ToString() == "EZUtils.Localization.GenerateLanguageAttribute")
                .Select(a =>
                {
                    CultureInfo cultureInfo = CultureInfo.GetCultureInfo((string)a.ConstructorArguments[0].Value);

                    string GetRule(string ruleKind)
                        => (string)a.NamedArguments.SingleOrDefault(kvp => kvp.Key == ruleKind).Value.Value;
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
                    //the original thought was to have this rooted to invocationOperation.Syntax.GetLocation().GetLineSpan().Path
                    //but that's kinda too complicated. it's up to the caller of GetTextExtractor to decide
                    string poFilePath = (string)a.ConstructorArguments[1].Value;

                    return (poFilePath, locale);
                }).ToList();

            return new InvocationParser(targets);
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
                PluralStatus = (countFound: true, PluralStatus.pluralId);
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
                    Context = value;
                    break;
                case LocalizationParameter.Id:
                    Id = value;
                    break;
                case LocalizationParameter.Other:
                    PluralStatus = (PluralStatus.countFound, pluralId: value);
                    break;
                default:
                    //we allow custom localization methods that have different parameters. for instance, EZLocalization's TranslateWindowTitle
                    //and we dont need every supported parameter anyway
                    break;
            }
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
}
