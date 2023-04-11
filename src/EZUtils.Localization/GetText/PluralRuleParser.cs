namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text.RegularExpressions;

    //https://github.com/unicode-org/cldr/blob/main/docs/ldml/tr35-numbers.md
    //https://github.com/unicode-org/cldr/blob/main/common/supplemental/plurals.xml

    internal static class PluralRuleParser
    {
        private const int ScaleShift = 16;
        private static readonly ParameterExpression operandsParameter = Expression.Parameter(typeof(Operands));

        public static Func<Operands, bool> Parse(string rule)
        {
            if (string.IsNullOrEmpty(rule)) return null;

            string condition = Regex.Match(rule, "^[^@]+").Value.Trim();
            //particularly for other kind
            if (string.IsNullOrWhiteSpace(condition)) return _ => true;
            int index = 0;

            Func<Operands, bool> parsedCondition = Expression.Lambda<Func<Operands, bool>>(ReadExpression(), operandsParameter).Compile();

            ValidateWithSamples(parsedCondition, rule);

            return parsedCondition;

            Expression ReadExpression()
            {
                Expression expression = ReadAndCondition();
                while (index < condition.Length && condition[index] == 'o' && condition[index + 1] == 'r')
                {
                    index += 2;
                    SkipSpaces();

                    expression = Expression.OrElse(expression, ReadAndCondition());
                }

                return expression;
            }

            Expression ReadAndCondition()
            {
                Expression andCondition = ReadRelation();
                while (index < condition.Length && condition[index] == 'a' && condition[index + 1] == 'n' && condition[index + 2] == 'd')
                {
                    index += 3;
                    SkipSpaces();

                    andCondition = Expression.AndAlso(andCondition, ReadRelation());
                }

                return andCondition;
            }

            Expression ReadRelation()
            {
                Expression expr = ReadExpr();

                bool isEqualityOperation;
                if (condition[index] == '=')
                {
                    isEqualityOperation = true;
                    index++;
                }
                else if (condition[index] == '!' && condition[index + 1] == '=')
                {
                    isEqualityOperation = false;
                    index += 2;
                }
                else throw new InvalidOperationException($"Expected '=' or '!='. Was '{condition.Substring(index, 2)}'.");
                SkipSpaces();

                Expression relation = ReadRangeListAndConstructRelation(expr, isEqualityOperation);

                while (index < condition.Length && condition[index] == ',')
                {
                    index++;
                    SkipSpaces();
                    relation = Expression.OrElse(relation, ReadRangeListAndConstructRelation(expr, isEqualityOperation));
                }

                return relation;
            }

            Expression ReadExpr()
            {
                string operandName = condition[index++].ToString();
                SkipSpaces();

                Expression expr =
                    operandName == "n" ? Expression.Field(operandsParameter, nameof(Operands.n)) :
                    operandName == "i" ? Expression.Field(operandsParameter, nameof(Operands.i)) :
                    operandName == "v" ? Expression.Field(operandsParameter, nameof(Operands.v)) :
                    operandName == "w" ? Expression.Field(operandsParameter, nameof(Operands.w)) :
                    operandName == "f" ? Expression.Field(operandsParameter, nameof(Operands.f)) :
                    operandName == "t" ? Expression.Field(operandsParameter, nameof(Operands.t)) :
                    //not supported and can only ever be zero
                    operandName == "c" ? Expression.Constant(0m) :
                    operandName == "e" ? (Expression)Expression.Constant(0m) :
                    throw new InvalidOperationException($"Unknown operand '{operandName}'");

                if (condition[index] == '%')
                {
                    index++;
                    SkipSpaces();

                    Expression value = ReadValue();
                    expr = Expression.Modulo(expr, value);
                }

                return expr;
            }

            Expression ReadRangeListAndConstructRelation(Expression expr, bool isEqualityOperation)
            {
                ConstantExpression firstValue = ReadValue();
                Expression rangeList;

                if (index < condition.Length && condition[index] == '.' && condition[index + 1] == '.')
                {
                    index += 2;
                    SkipSpaces(); //may not be necessary idk

                    ConstantExpression secondValue = ReadValue();
                    rangeList = Expression.AndAlso(
                        Expression.GreaterThanOrEqual(expr, firstValue),
                        Expression.LessThanOrEqual(expr, secondValue));
                }
                else
                {
                    rangeList = Expression.Equal(expr, firstValue);
                }

                if (!isEqualityOperation)
                {
                    rangeList = Expression.Not(rangeList);
                }

                return rangeList;
            }

            ConstantExpression ReadValue()
            {
                if (index >= condition.Length) throw new InvalidOperationException("Expected digit. Got to end of condition.");
                //we still start at 0 in order to check for digit
                int count = 0;

                while (index + count < condition.Length && char.IsDigit(condition, index + count)) count++;
                if (count == 0) throw new InvalidOperationException($"Expected digit. Got '{condition[index]}'.");

                ConstantExpression valueExpression = Expression.Constant(decimal.Parse(condition.Substring(index, count), CultureInfo.InvariantCulture));
                index += count;
                SkipSpaces();
                return valueExpression;
            }

            void SkipSpaces()
            {
                while (index < condition.Length && char.IsWhiteSpace(condition[index])) index++;
            }
        }
        private static void ValidateWithSamples(Func<Operands, bool> parsedCondition, string raw)
        {
            Match match = Regex.Match(raw, "(?:@integer([^@]+))?(?:@decimal(.+))?$");

            //we dont really care about the integer or decimal labels, since the values necessarily have a decimal point (or not)
            IEnumerable<decimal> testSet = Enumerable
                .Range(1, 2)
                .SelectMany(i => match.Groups[i].Value
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()))
                .SelectMany(s => ParseRawSingleSample(s));
            foreach (decimal value in testSet)
            {
                Operands operands = new Operands(value);
                if (!parsedCondition(operands)) throw new InvalidOperationException($"Failed condition test on sample '{value}'.");
            }
        }

        private static readonly decimal[] increments =
            new decimal[29] { 1e-00m, 1e-01m, 1e-02m, 1e-03m, 1e-04m, 1e-05m, 1e-06m, 1e-07m, 1e-08m, 1e-09m, 1e-10m, 1e-11m, 1e-12m, 1e-13m, 1e-14m, 1e-15m, 1e-16m, 1e-17m, 1e-18m, 1e-19m, 1e-20m, 1e-21m, 1e-22m, 1e-23m, 1e-24m, 1e-25m, 1e-26m, 1e-27m, 1e-28m };
        private static IEnumerable<decimal> ParseRawSingleSample(string sample)
        {
            if (sample == "..." || sample == "…") yield break;

            decimal[] split = sample
                .Split('~')
                .Select(s => s.Trim())
                //we can't support these property, as we cant support the operands c and e
                .Where(s => !s.Contains('e') && !s.Contains('c'))
                .Select(s => decimal.Parse(s, CultureInfo.InvariantCulture))
                .ToArray();
            if (split.Length == 0) yield break;
            if (split.Length == 1)
            {
                yield return split[0];
                yield break;
            }

            int decimalDigits = decimal.GetBits(split[0])[3] >> ScaleShift & 31;
            decimal increment = increments[decimalDigits];
            for (decimal i = split[0]; i <= split[1]; i += increment)
            {
                yield return i;
            }
        }
    }
}
