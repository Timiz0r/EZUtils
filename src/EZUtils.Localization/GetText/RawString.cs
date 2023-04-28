namespace EZUtils.Localization
{
    using System;

    //in order to support having string and FormattableString overloads, we need this thing with an implicit conversion
    //from string and not an implicit conversion to FormattableString.
    //the (readonly) ref struct just keeps things performant, though certainly with mild restrictions (like accessing
    //the underling value in linq or other lambdas).
    //
    //it's worth noting early designs saw many more T overloads that differed based on RawString vs FormattableString
    //but now it's been slimmed down to few usages of this struct. even still, it's worth having.
    public readonly ref struct RawString
    {
        public string Value { get; }

        public RawString(string str)
        {
            Value = str;
        }

#pragma warning disable
        public static implicit operator RawString(string s) => new RawString(s);

        public static implicit operator RawString(FormattableString formattableString)
            => throw new InvalidOperationException("This never gets hit.");
#pragma warning restore
    }
}
