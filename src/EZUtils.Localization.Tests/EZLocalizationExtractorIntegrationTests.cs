namespace EZUtils.Localization.Tests.Integration
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine.TestTools;

    //NOTE: perhaps in conjuction with breakpoints, or perhaps not, debugging can be tempermental
    //additionally, domain reloading seems to clear out locals, hence why, in some cases, we recompute values
    //  via local functions instead of normal locals. in general, avoid accessing locals after a domain reload!
    public class EZLocalizationExtractorIntegrationTests
    {
        private const string TestArtifactRootFolder = "Packages/com.timiz0r.ezutils.localization.tests/IntegrationTestGen";
        private const string LocaleSynchronizationKey = "EZLocalizationExtractorIntegrationTests";

        [TearDown]
        public void TearDown()
        {
            _ = AssetDatabase.DeleteAsset(TestArtifactRootFolder);
            //a leaky implementation detail to be aware of
            EditorPrefs.DeleteKey("EZUtils.Localization.SelectedLocale." + LocaleSynchronizationKey);
        }
        [SetUp]
        public void SetUp() => _ = Directory.CreateDirectory(TestArtifactRootFolder);

        [UnityTest]
        public IEnumerator AutomatedExtraction_DoesAbsolutelyNothing_IfNoAssemblyAttribute()
        {
            string languageAttribute = GenerateLocalizationAttribute(
                "IntegrationTestGen/ja-integrationtest.po",
                new Locale(CultureInfo.GetCultureInfo("ja"),
                    new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")));
            GenerateEmptyProxyType(languageAttribute);
            GenerateTestAction(
                usings: string.Empty,
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute),
                code: @"
            result.Add(loc.T(""foo""));
            result.Add(loc.SelectLocaleOrNative(CultureInfo.GetCultureInfo(""ja"")) == Locale.English ? ""isNative"" : ""isNotNative"");
            ");

            //TODO: yet to be determined if we need another refresh or not for proxy. probably do.
            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();
            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(
                File.Exists(Path.Combine(TestArtifactRootFolder, "ja-integrationtest.po")),
                Is.False);
            Assert.That(typeof(Localization).GetMethods(), Has.None.Matches<MethodInfo>(m => m.Name == "T"));
            Assert.That(result, Is.EqualTo(new[] { "foo", "isNative" }));
        }

        [UnityTest]
        public IEnumerator AutomatedExtraction_ExtractsInstanceInvocations()
        {
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("IntegrationTestGen/ja-integrationtest.po", locale());
            GenerateTestAction(
                usings: string.Empty,
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute),
                code: @"
            loc.SelectLocale(CultureInfo.GetCultureInfo(""ja""));
            result.Add(loc.T(""foo""));
            decimal value = 1m;
            result.Add(loc.T($""{value} foo"", value, $""{value} foos""));

            loc.SelectLocaleOrNative();
            result.Add(loc.T(""foo""));
            result.Add(loc.T($""{value} foo"", value, $""{value} foos""));
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            Assert.That(
                File.Exists(Path.Combine(TestArtifactRootFolder, "ja-integrationtest.po")),
                Is.False);
            _ = new GetTextCatalogBuilder()
                .ForPoFile("ja-integrationtest.po", locale(), d => d
                    .AddEntry(e => e
                        .ConfigureId("foo")
                        .ConfigureValue("ja:foo"))
                    .AddEntry(e => e
                        .ConfigureId("{0} foo")
                        .ConfigureAsPlural("{0} foos")
                        .ConfigureValue("ja:{0} foo")
                        .ConfigureAdditionalPluralValue("ja:{0} foos")))
                .WriteToDisk(TestArtifactRootFolder);

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "ja:foo",
                "ja:1 foos",
                "foo",
                "1 foo"
            }));
        }

        private static void GenerateAssemblyAttribute()
        => File.WriteAllText(
            Path.Combine(TestArtifactRootFolder, "Assembly.cs"),
            "[assembly: EZUtils.Localization.LocalizedAssembly]");

        private static void GenerateEmptyProxyType(params string[] languageAttributes)
            => File.WriteAllText(Path.Combine(TestArtifactRootFolder, "Localization.cs"), $@"
namespace EZUtils.Localization.Tests.Integration
{{
    using EZUtils.Localization;

    [LocalizationProxy]
    {string.Join("\n    ", languageAttributes)}
    public static partial class Localization
    {{
    }}
}}");
        private static void GenerateTestAction(string usings, string code)
            => GenerateTestAction(usings, string.Empty, code);
        private static void GenerateTestAction(string usings, string localizationFieldDeclaration, string code)
            => File.WriteAllText(Path.Combine(TestArtifactRootFolder, "IntegrationTestAction.cs"), $@"
namespace EZUtils.Localization.Tests.Integration
{{
    using EZUtils.Localization;
    using System.Globalization;
    using System.Collections.Generic;
    using static Localization;
    {usings}

    public partial class IntegrationTestAction
    {{
        {localizationFieldDeclaration}

        private IReadOnlyList<string> ExecuteImpl()
        {{
            List<string> result = new List<string>();

            {code}

            return result;
        }}
    }}
}}");

        private static string GenerateLocalizationAttribute(string poFilePath, Locale locale)
        {
            //[GenerateLanguage(""ja"", ""ja-inttest.po"", Other = "" @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"")]
            StringBuilder sb = new StringBuilder()
                .Append("[GenerateLanguage(\"")
                .Append(locale.CultureInfo.Name)
                .Append(@""", """)
                .Append(poFilePath)
                .Append('"');

            AddPluralRule("Zero", locale.PluralRules.Zero);
            AddPluralRule("One", locale.PluralRules.One);
            AddPluralRule("Two", locale.PluralRules.Two);
            AddPluralRule("Few", locale.PluralRules.Few);
            AddPluralRule("Many", locale.PluralRules.Many);
            AddPluralRule("Other", locale.PluralRules.Other);

            _ = sb.Append(")]");
            return sb.ToString();

            void AddPluralRule(string kind, string rule)
            {
                if (rule != null)
                    _ = sb.Append(@", ").Append(kind).Append(@" = """).Append(rule).Append('"');
            }
        }
        private static string GenerateLocalizationFieldDeclaration(params string[] languageAttributes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string languageAttribute in languageAttributes)
            {
                sb.Append(languageAttribute).AppendLine();
            }
            _ = sb.Append($@"private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder(""{TestArtifactRootFolder}"", ""{LocaleSynchronizationKey}"");");
            return sb.ToString();
        }
    }
}

//a convenient place to write test action code with intellisense
//just parts of code into GenerateTestAction call
// namespace EZUtils.Localization.Tests.Integration
// {
//     using System.Collections.Generic;
//     using System.Globalization;
//     using EZUtils.Localization;
//     using static Localization;

//     public partial class IntegrationTestAction
//     {
//         [GenerateLanguage("ja", "ja-inttest.po", Other = " @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")]
//         [GenerateLanguage("ko", "ko-inttest.po", Other = " @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")]
//         private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder("Packages/com.timiz0r.ezutils.localization/IntegrationTestGen", "EZLocalizationExtractorIntegrationTests");

//         private IReadOnlyList<string> ExecuteImpl()
//         {
//             List<string> result = new List<string>();

//             //
//             result.Add(loc.T("foo"));
//             result.Add(loc.SelectLocaleOrNative(CultureInfo.GetCultureInfo("ja")) == Locale.English ? "isNative" : "isNotNative");

//             return result;
//         }
//     }
// }

namespace EZUtils.Localization.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using NUnit.Framework;

    public partial class IntegrationTestAction
    {
        //integration tests will generate the other portion of this class
        //in future c# versions, we can generate partial methods (i believe unity 2021 would do the trick)
        //until then, we need reflection
        //
        //design-wise, we could make this a void method and add asserts to the generated ExecuteImpl
        //we currently return a list so that we can more easily write Asserts in a place we get intellisense
        //one interesting alternative design would be to hide cs file from unity in a folder, then
        //copy them to the gen folder as needed. currently opting to write all code here, at this risk of build errors.
        public IReadOnlyList<string> Execute()
        {
            MethodInfo implMethod = GetType().GetMethod("ExecuteImpl", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(implMethod, Is.Not.Null);
            Assert.That(implMethod.ReturnType, Is.EqualTo(typeof(IReadOnlyList<string>)));

            IReadOnlyList<string> result = (IReadOnlyList<string>)implMethod.Invoke(this, Array.Empty<object>());
            return result;
        }
    }

    //obviously cant invoke anything from this class until after we generate a proxy
    //this just allows the using static to work
    public static partial class Localization
    {

    }
}
