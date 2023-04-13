namespace EZUtils.Localization
{
    using System;

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
    public sealed class LocalizationParameterAttribute : Attribute
    {
        public string ParameterName { get; }

        public LocalizationParameterAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }
    }

    public static class LocalizationParameter
    {
        //if we want to make this more strongly typed later, avoid enum since it'll change often
        public const string Context = "context";
        public const string Id = "id";
        public const string Count = "count";
        public const string Zero = "zero";
        public const string Two = "two";
        public const string Few = "few";
        public const string Many = "many";
        public const string Other = "other";
        public const string SpecialZero = "specialZero";
    }
}
