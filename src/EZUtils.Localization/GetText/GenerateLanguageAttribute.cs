namespace EZUtils.Localization
{
    using System;

    //the original idea was to provide a list of dummy args to ezlocalization
    //but since they'd never be touched, it kinda makes more sense to go attributes
    //i mean, it would probably be nice to support validating that they exist when loading
    //but it would then be hard/impossible to extract them out of code to generate them
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class GenerateLanguageAttribute : Attribute
    {
        public string CultureInfoCode { get; }
        public string PoFilePath { get; }

        public string Zero { get; set; }
        public string One { get; set; }
        public string Two { get; set; }
        public string Few { get; set; }
        public string Many { get; set; }
        public string Other { get; set; }

        public GenerateLanguageAttribute(string cultureInfoCode, string poFilePath)
        {
            CultureInfoCode = cultureInfoCode;
            PoFilePath = poFilePath;
        }
    }
}
