namespace EZUtils.Localization
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Microsoft.CodeAnalysis;

    internal static class GenerateLanguageAttributeParser
    {
        public static IReadOnlyList<(string poFilePath, Locale locale)> ParseTargets(IEnumerable<AttributeData> attributes)
        {
            AttributeData[] generateLanguageAttributes = attributes
                .Where(a => a.AttributeClass.ToString() == "EZUtils.Localization.GenerateLanguageAttribute")
                .ToArray();

            (string poFilePath, Locale locale)[] languages = attributes
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

                    Locale locale = new Locale(cultureInfo, pluralRules);
                    //the original thought was to have this rooted to invocationOperation.Syntax.GetLocation().GetLineSpan().Path
                    //but that's kinda too complicated. it's up to the caller of GetTextExtractor to decide roots
                    string poFilePath = (string)a.ConstructorArguments[1].Value;

                    return (poFilePath, locale);
                }).ToArray();
            return languages;
        }
    }
}