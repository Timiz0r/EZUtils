namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;

    public class PluralRules
    {
        private readonly Dictionary<string, RuleSet> rules = new Dictionary<string, RuleSet>();

        private PluralRules(Dictionary<string, RuleSet> rules)
        {
            this.rules = rules;
        }

        public PluralType Evaluate(CultureInfo cultureInfo, decimal value)
        {
            RuleSet ruleSet = rules[cultureInfo.TwoLetterISOLanguageName];
            PluralType result = ruleSet.Evaluate(value);
            return result;
        }

        public static PluralRules LoadFrom(XDocument document)
        {
            Dictionary<string, RuleSet> rules = document.Root
                .Element("plurals")
                .Elements("pluralRules")
                .SelectMany(pr =>
                {
                    string[] locales = pr.Attribute("locales").Value.Split(' ');
                    RuleSet ruleSet = new RuleSet(
                        zero: Parse("zero"),
                        one: Parse("one"),
                        two: Parse("two"),
                        few: Parse("few"),
                        many: Parse("many")
                    );
                    return locales.Select(l => (l, ruleSet));

                    Func<Operands, bool> Parse(string kind)
                        => PluralRuleParser.Parse(pr.Elements("pluralRule").SingleOrDefault(e => e.Attribute("count")?.Value == kind)?.Value);
                })
                .ToDictionary(e => e.l, e => e.ruleSet, StringComparer.OrdinalIgnoreCase);

            return new PluralRules(rules);
        }

        private class RuleSet
        {
            private readonly Func<Operands, bool> Zero;
            private readonly Func<Operands, bool> One;
            private readonly Func<Operands, bool> Two;
            private readonly Func<Operands, bool> Few;
            private readonly Func<Operands, bool> Many;

            public RuleSet(
                Func<Operands, bool> zero,
                Func<Operands, bool> one,
                Func<Operands, bool> two,
                Func<Operands, bool> few,
                Func<Operands, bool> many)
            {
                Zero = zero;
                One = one;
                Two = two;
                Few = few;
                Many = many;
            }

            public PluralType Evaluate(decimal value)
            {
                Operands operands = new Operands(value);
                if (Zero?.Invoke(operands) == true) return PluralType.Zero;
                if (One?.Invoke(operands) == true) return PluralType.One;
                if (Two?.Invoke(operands) == true) return PluralType.Two;
                if (Few?.Invoke(operands) == true) return PluralType.Few;
                if (Many?.Invoke(operands) == true) return PluralType.Many;
                return PluralType.Other;
            }
        }
    }
}
