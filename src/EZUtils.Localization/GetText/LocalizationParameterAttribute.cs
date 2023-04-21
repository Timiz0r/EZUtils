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
}
