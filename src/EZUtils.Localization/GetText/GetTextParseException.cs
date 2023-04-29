namespace EZUtils.Localization
{
    [System.Serializable]
    public class GetTextParseException : System.Exception
    {
        public GetTextParseException() { }
        public GetTextParseException(string message) : base(message) { }
        public GetTextParseException(string message, System.Exception inner) : base(message, inner) { }
        protected GetTextParseException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
