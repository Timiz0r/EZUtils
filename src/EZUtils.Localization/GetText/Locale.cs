namespace EZUtils.Localization
{
    using System;
    using System.Globalization;

    //even though it's possible for two Locales to have the same culture but different plural rules
    //we still consider the culture to be the sole identifying property
    public class Locale : IEquatable<Locale>
    {
        public static Locale English { get; } = new Locale(
            CultureInfo.GetCultureInfo("en"),
            new PluralRules(
                one: "i = 1 and v = 0 @integer 1",
                other: " @integer 0, 2~16, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));

        public CultureInfo CultureInfo { get; }
        public PluralRules PluralRules { get; }

        public Locale(CultureInfo cultureInfo, PluralRules pluralRules)
        {
            CultureInfo = cultureInfo;
            PluralRules = pluralRules;
        }

        public Locale(CultureInfo cultureInfo)
            : this(cultureInfo, PluralRules.GetDefault(cultureInfo))
        {
        }

        public Locale WithZeroPluralRule() => new Locale(
            CultureInfo,
            new PluralRules(
                zero: "n = 0",
                one: PluralRules.One,
                two: PluralRules.Two,
                few: PluralRules.Few,
                many: PluralRules.Many,
                other: PluralRules.Other));

        public override bool Equals(object obj)
            => obj is Locale locale && Equals(locale);
        public bool Equals(Locale other) => other?.CultureInfo == CultureInfo;
        public override int GetHashCode() => CultureInfo.GetHashCode();
        public static bool operator ==(Locale lhs, Locale rhs)
            => ReferenceEquals(lhs, rhs)
            || (!ReferenceEquals(lhs, null) && lhs.Equals(rhs));
        public static bool operator !=(Locale lhs, Locale rhs) => !(lhs == rhs);
    }
}
