namespace EZUtils.Localization
{
    internal readonly struct ExtractedString
    {
        public readonly string Value;
        public readonly string OriginalFormat;

        public ExtractedString(string value) : this(value, null) { }

        public ExtractedString(string value, string originalFormat)
        {
            Value = value;
            OriginalFormat = originalFormat;
        }
    }
}
