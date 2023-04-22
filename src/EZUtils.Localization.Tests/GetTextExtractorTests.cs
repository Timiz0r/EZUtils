namespace EZUtils.Localization.Tests
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using NUnit.Framework;

    public class GetTextExtractorTests
    {
        [Test]
        public void CreatesDocuments_ForEachLangugeAttribute()
        {
            string locDefinition = $@"
            [GenerateLanguage(""ja"", ""unittest-ja.po"", UseSpecialZero = true, Zero = ""@integer 0"", One = ""@integer 1"", Two = ""@integer 2"", Few = ""@integer 3"", Many = ""@integer 4"", Other = "" @integer 5"")]
            [GenerateLanguage(""ko"", ""unittest-ko.po"", UseSpecialZero = false, Few = ""@integer 0"", Other = ""@integer 1"")]
            private static readonly GetTextCatalog loc;";
            string code = @"
                loc.T(""foo bar baz"");";

            GetTextCatalogBuilder catalogBuilder = Extract(locDefinition, code);
            IReadOnlyList<GetTextDocument> documents = catalogBuilder.GetDocuments();
            GetTextDocument jaDocument = documents.Single(d => d.Header.Locale.CultureInfo.Name == "ja");
            GetTextDocument koDocument = documents.Single(d => d.Header.Locale.CultureInfo.Name == "ko");

            Assert.That(jaDocument.Header.Locale.CultureInfo, Is.EqualTo(CultureInfo.GetCultureInfo("ja")));
            Assert.That(jaDocument.Header.Locale.UseSpecialZero, Is.True);
            Assert.That(jaDocument.Header.Locale.PluralRules.Zero, Is.EqualTo("@integer 0"));
            Assert.That(jaDocument.Header.Locale.PluralRules.One, Is.EqualTo("@integer 1"));
            Assert.That(jaDocument.Header.Locale.PluralRules.Two, Is.EqualTo("@integer 2"));
            Assert.That(jaDocument.Header.Locale.PluralRules.Few, Is.EqualTo("@integer 3"));
            Assert.That(jaDocument.Header.Locale.PluralRules.Many, Is.EqualTo("@integer 4"));
            Assert.That(jaDocument.Header.Locale.PluralRules.Other, Is.EqualTo("@integer 5"));

            Assert.That(koDocument.Header.Locale.CultureInfo, Is.EqualTo(CultureInfo.GetCultureInfo("ko")));
            Assert.That(koDocument.Header.Locale.UseSpecialZero, Is.False);
            Assert.That(koDocument.Header.Locale.PluralRules.Zero, Is.Null);
            Assert.That(koDocument.Header.Locale.PluralRules.One, Is.Null);
            Assert.That(koDocument.Header.Locale.PluralRules.Two, Is.Null);
            Assert.That(koDocument.Header.Locale.PluralRules.Few, Is.EqualTo("@integer 0"));
            Assert.That(koDocument.Header.Locale.PluralRules.Many, Is.Null);
            Assert.That(koDocument.Header.Locale.PluralRules.Other, Is.EqualTo("@integer 1"));
        }

        [Test]
        public void ExtractsBasicInvocation()
        {
            string code = @"
                loc.T(""foo bar baz"");";

            GetTextCatalogBuilder catalogBuilder = Extract(BasicLocDefinition, code);
            GetTextDocument document = catalogBuilder.GetDocuments()[0];

            Assert.That(document.Entries.Count, Is.EqualTo(2));
            AssertHasEntry(document, context: null, id: "foo bar baz");
        }

        [Test]
        public void ExtractsContextualInvocation()
        {
            string code = @"
                loc.T(""ctx"", ""foo bar baz"");";

            GetTextCatalogBuilder catalogBuilder = Extract(BasicLocDefinition, code);
            GetTextDocument document = catalogBuilder.GetDocuments()[0];

            Assert.That(document.Entries.Count, Is.EqualTo(2));
            AssertHasEntry(document, context: "ctx", id: "foo bar baz");
        }

        [Test]
        public void ExtractsPluralInvocation()
        {
            string locDefinition = $@"
                [GenerateLanguage(""ja"", ""unittest-ja.po"", UseSpecialZero = true, Zero = ""@integer 0"", One = ""@integer 1"", Two = ""@integer 2"", Few = ""@integer 3"", Many = ""@integer 4"", Other = "" @integer 5"")]
                private static readonly GetTextCatalog loc;";
            string code = @"
                loc.T(
                    $""foo bar baz"",
                    1m,
                    other: $""other"",
                    specialZero: ""special zero"",
                    zero: $""zero"",
                    two: $""two"",
                    few: $""few"",
                    many: $""many"");";

            GetTextCatalogBuilder catalogBuilder = Extract(locDefinition, code);
            GetTextDocument document = catalogBuilder.GetDocuments()[0];
            GetTextEntry entry = document.Entries[1];

            Assert.That(document.Entries.Count, Is.EqualTo(2));
            Assert.That(entry.Id, Is.EqualTo("foo bar baz"));
            Assert.That(entry.PluralId, Is.EqualTo("other"));
            Assert.That(entry.PluralValues.Count, Is.EqualTo(7));
            Assert.That(entry.PluralValues, Is.All.Empty);
        }

        [Test]
        public void ExtractsInvocation_WhenCallingAnnotatedMethod()
        {
            string code = @"
                namespace Foo
                {
                    using EZUtils.Localization;
                    public class Bar
                    {
                        [GenerateLanguage(""ja"", ""unittest-ja.po"", UseSpecialZero = true, Other = "" @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"")]
                        private static readonly Localization loc = null;
                        public static void Baz()
                        {
                            loc.TranslateDisFool(""foo bar baz"");
                        }
                    }

                    public class Localization
                    {
                        [LocalizationMethod]
                        public void TranslateDisFool(string id) { }
                    }
                }";
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            IGetTextExtractionWorkRunner workRunner = GetTextExtractionWorkRunner.CreateSynchronous();
            GetTextExtractor extractor = new GetTextExtractor(compilation => compilation, workRunner);

            extractor.AddSource(source: code, displayPath: "Bar.cs", catalogRoot: string.Empty);
            extractor.Extract(catalogBuilder);
            GetTextDocument document = catalogBuilder.GetDocuments()[0];

            Assert.That(document.Entries.Count, Is.EqualTo(2));
            AssertHasEntry(document, context: null, id: "foo bar baz");
        }

        [Test]
        public void ExtractsInvocation_WhenCallingAnnotatedMethodAndParameter()
        {
            string code = @"
                namespace Foo
                {
                    using EZUtils.Localization;
                    public class Bar
                    {
                        [GenerateLanguage(""ja"", ""unittest-ja.po"", UseSpecialZero = true, Other = "" @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"")]
                        private static readonly Localization loc = null;
                        public static void Baz()
                        {
                            loc.TranslateDisFool(""foo bar baz"");
                        }
                    }

                    public class Localization
                    {
                        [LocalizationMethod]
                        public void TranslateDisFool([LocalizationParameter(LocalizationParameter.Id)] string lol) { }
                    }
                }";
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            IGetTextExtractionWorkRunner workRunner = GetTextExtractionWorkRunner.CreateSynchronous();
            GetTextExtractor extractor = new GetTextExtractor(compilation => compilation, workRunner);

            extractor.AddSource(source: code, displayPath: "Bar.cs", catalogRoot: string.Empty);
            extractor.Extract(catalogBuilder);
            GetTextDocument document = catalogBuilder.GetDocuments()[0];

            Assert.That(document.Entries.Count, Is.EqualTo(2));
            AssertHasEntry(document, context: null, id: "foo bar baz");
        }

        [Test]
        public void SkipsInvocations_WhenThereIsANonConstantStringValue()
        {
            string code = @"
                string something = 1.ToString();
                loc.T(something);";

            GetTextCatalogBuilder catalogBuilder = Extract(BasicLocDefinition, code);
            GetTextDocument document = catalogBuilder.GetDocuments()[0];

            Assert.That(document.Entries.Count, Is.EqualTo(1));
        }

        [Test]
        public void SkipsInvocations_WhenThereIsANonConstantCountValue()
        {
            string code = @"
                string something = 1.ToString();
                loc.T(something, (decimal)""a"".Length, ""foo"");";

            GetTextCatalogBuilder catalogBuilder = Extract(BasicLocDefinition, code);
            GetTextDocument document = catalogBuilder.GetDocuments()[0];

            Assert.That(document.Entries.Count, Is.EqualTo(1));
        }

        private static void AssertHasEntry(GetTextDocument document, string context, string id) => Assert.That(
            document.Entries, Has.Exactly(1).Matches<GetTextEntry>(e => e.Context == context && e.Id == id));

        private static readonly string BasicLocDefinition = @"
            [GenerateLanguage(""ja"", ""unittest-ja.po"", UseSpecialZero = true, Other = "" @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"")]
            private static readonly GetTextCatalog loc;";
        private static GetTextCatalogBuilder Extract(string locDefinition, string code)
        {
            code = $@"
                namespace Foo
                {{
                    using EZUtils.Localization;
                    public class Bar
                    {{
                        {locDefinition}
                        public static void Baz()
                        {{
                            {code}
                        }}
                    }}
                }}";

            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            IGetTextExtractionWorkRunner workRunner = GetTextExtractionWorkRunner.CreateSynchronous();
            GetTextExtractor extractor = new GetTextExtractor(compilation => compilation, workRunner);

            extractor.AddSource(source: code, displayPath: "Bar.cs", catalogRoot: string.Empty);
            extractor.Extract(catalogBuilder);

            return catalogBuilder;
        }
    }
}
