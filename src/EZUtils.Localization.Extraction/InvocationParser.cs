namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Operations;

    internal class InvocationParser
    {
        private static readonly InvocationParser NonLocalized = new InvocationParser(Array.Empty<(string poFilePath, Locale locale)>())
        {
            Success = false
        };

        public IReadOnlyList<(string poFilePath, Locale locale)> Targets { get; }
        public string Context { get; private set; }
        public ExtractedString Id { get; private set; }
        //it's overall relatively hard to not have a successful extraction, so we default to true
        public bool Success { get; private set; } = true;
        public (bool countFound, ExtractedString pluralId) PluralStatus { get; private set; }

        private InvocationParser(IReadOnlyList<(string poFilePath, Locale locale)> targets)
        {
            Targets = targets;
        }

        public static InvocationParser ForInvocation(IInvocationOperation invocationOperation)
        {
            ImmutableArray<AttributeData> targetAttributes;
            if (invocationOperation.Instance is IMemberReferenceOperation memberReferenceOperation)
            {
                targetAttributes = memberReferenceOperation.Member.GetAttributes();
            }
            else if (invocationOperation.Instance == null)
            {
                targetAttributes = invocationOperation.TargetMethod.ContainingType.GetAttributes();
            }
            else if (invocationOperation.Instance is IInstanceReferenceOperation)
            {
                //unless I'm mistaken, is calling a member
                //we don't allow localization methods to be localized themselves, so these arent candidates for localization
                return NonLocalized;
            }
            else throw new InvalidOperationException(
                $"Unable to extract any attributes in order to find catalog generation attributes.");

            IReadOnlyList<(string poFilePath, Locale locale)> targets =
                GenerateLanguageAttributeParser.ParseTargets(targetAttributes);
            if (targets.Count == 0) return NonLocalized;

            InvocationParser invocationParser = new InvocationParser(targets);
            foreach (IArgumentOperation argument in invocationOperation.Arguments)
            {
                try
                {
                    invocationParser.HandleArgument(argument);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to parse invocation: {invocationOperation.Syntax}", ex);
                }
            }

            return invocationParser;
        }

        private void HandleArgument(IArgumentOperation argument)
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

            if (!TryGetString(argument, out ExtractedString value))
            {
                Success = false;
            }
            switch (parameterName)
            {
                case LocalizationParameter.Context:
                    Context = value.Value;
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

        private static bool TryGetString(IOperation operation, out ExtractedString result)
        {
            if (operation is IArgumentOperation argumentOperation)
            {
                return TryGetString(argumentOperation.Value, out result);
            }

            if (operation is IConversionOperation conversionOperation)
            {
                return TryGetString(conversionOperation.Operand, out result);
            }

            if (operation is ILiteralOperation literalOperation && literalOperation.Type.ToString() == "string")
            {
                result = new ExtractedString((string)literalOperation.ConstantValue.Value);
                return true;
            }

            if (operation is IInterpolatedStringOperation interpolatedStringOperation)
            {
                StringBuilder interpolationStringBuilder = new StringBuilder();
                int formatIndex = 0;
                foreach (IInterpolatedStringContentOperation part in interpolatedStringOperation.Parts)
                {
                    if (part is IInterpolatedStringTextOperation textOperation
                        && TryGetString(textOperation.Text, out ExtractedString textOperationString))
                    {
                        _ = interpolationStringBuilder.Append(textOperationString.Value);
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

                result = new ExtractedString(
                    value: interpolationStringBuilder.ToString(),
                    //a previous design had an "0=`expression`" format, but decided to keep the string for better readability
                    originalFormat: ((InterpolatedStringExpressionSyntax)interpolatedStringOperation.Syntax).Contents.ToString());
                return true;
            }

            if (operation is IBinaryOperation binaryOperation
                && binaryOperation.OperatorKind == BinaryOperatorKind.Add
                && TryGetString(binaryOperation.LeftOperand, out ExtractedString leftOperandString)
                && TryGetString(binaryOperation.RightOperand, out ExtractedString rightOperandString))
            {
                result = new ExtractedString(
                    value: string.Concat(leftOperandString.Value, rightOperandString.Value),
                    originalFormat: string.Concat(leftOperandString.OriginalFormat, rightOperandString.OriginalFormat));
                return true;
            }

            if (operation is IDefaultValueOperation)
            {
                result = default;
                return true;
            }

            if (operation is IFieldReferenceOperation fieldReferenceOperation
                && fieldReferenceOperation.ConstantValue.HasValue)
            {
                //if (!fieldReferenceOperation.ConstantValue.HasValue) throw new ArgumentOutOfRangeException(
                //    nameof(operation),
                //    $"{fieldReferenceOperation.Type} is not a constant. Reference: {fieldReferenceOperation.Syntax}");

                result = new ExtractedString((string)fieldReferenceOperation.ConstantValue.Value);
                return true;
            }

            result = default;
            return false;
            //throw new ArgumentOutOfRangeException(nameof(operation), $"Do not know how to handle extracting string from '{operation.Kind}'. Current operation: {operation.Syntax}");
        }
    }
}
