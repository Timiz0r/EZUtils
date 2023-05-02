namespace EZUtils.Localization.Tests.Integration
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine.TestTools;

    //NOTE: perhaps in conjuction with breakpoints, or perhaps not, debugging can be tempermental
    //additionally, domain reloading seems to clear out locals, hence why, in some cases, we recompute values
    //  via local functions instead of normal locals. in general, avoid accessing locals after a domain reload!
    public class EZLocalizationExtractorIntegrationTests
    {
        private const string TestAssemblyRootFolder = "Packages/com.timiz0r.ezutils.localization.tests/IntegrationTestAssembly/";
        private const string LocaleSynchronizationKey = "EZLocalizationExtractorIntegrationTests";

        [TearDown]
        public void TearDown()
        {
            _ = AssetDatabase.DeleteAsset(TestAssemblyRootFolder + "Gen");
            //a leaky implementation detail to be aware of
            EditorPrefs.DeleteKey("EZUtils.Localization.SelectedLocale." + LocaleSynchronizationKey);
        }

        [SetUp]
        public void SetUp() => _ = Directory.CreateDirectory(TestAssemblyRootFolder + "Gen");

        [UnityTest]
        public IEnumerator AutomatedExtraction_DoesAbsolutelyNothing_IfNoAssemblyAttribute()
        {
            string languageAttribute = GenerateLocalizationAttribute(
                "Gen/ja-integrationtest.po",
                new Locale(CultureInfo.GetCultureInfo("ja"),
                    new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")));
            GenerateEmptyProxyType(languageAttribute);
            GenerateTestAction(
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute),
                code: @"
            result.Add(loc.T(""foo""));
            result.Add(loc.SelectLocaleOrNative(CultureInfo.GetCultureInfo(""ja"")) == Locale.English ? ""isNative"" : ""isNotNative"");
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();
            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(
                File.Exists(Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po")),
                Is.False);
            Assert.That(typeof(Localization).GetMethods(), Has.None.Matches<MethodInfo>(m => m.Name == "T"));
            Assert.That(result, Is.EqualTo(new[] { "foo", "isNative" }));
        }

        [UnityTest]
        public IEnumerator AutomatedExtraction_ExtractsInstanceInvocations()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());
            GenerateTestAction(
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

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            Assert.That(File.Exists(poFilePath), Is.True);
            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath, locale(), d => d
                    .OverwriteEntry(e => e
                        .ConfigureId("foo")
                        .ConfigureValue("ja:foo"))
                    .OverwriteEntry(e => e
                        .ConfigureId("{0} foo")
                        .ConfigureAsPlural("{0} foos")
                        .ConfigureAdditionalPluralValue("ja:{0} foos")))
                .WriteToDisk();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "ja:foo",
                "ja:1 foos",
                "foo",
                "1 foo"
            }));
        }

        [UnityTest]
        public IEnumerator AutomatedExtraction_ExtractsProxyInvocations()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());
            GenerateEmptyProxyType(languageAttribute);

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            GenerateTestAction(
                code: @"
            SelectLocale(CultureInfo.GetCultureInfo(""ja""));
            result.Add(T(""foo""));
            decimal value = 1m;
            result.Add(T($""{value} foo"", value, $""{value} foos""));

            SelectLocaleOrNative();
            result.Add(T(""foo""));
            result.Add(T($""{value} foo"", value, $""{value} foos""));
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            Assert.That(File.Exists(poFilePath), Is.True);
            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath, locale(), d => d
                    .OverwriteEntry(e => e
                        .ConfigureId("foo")
                        .ConfigureValue("ja:foo"))
                    .OverwriteEntry(e => e
                        .ConfigureId("{0} foo")
                        .ConfigureAsPlural("{0} foos")
                        .ConfigureAdditionalPluralValue("ja:{0} foos")))
                .WriteToDisk();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "ja:foo",
                "ja:1 foos",
                "foo",
                "1 foo"
            }));
        }

        [UnityTest]
        public IEnumerator EZLocalization_AutoRetranslates_WhenLocaleChanged()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());
            GenerateEmptyProxyType(languageAttribute);

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            GenerateTestAction(
                localizationFieldDeclaration: string.Empty,
                code: @"
            Label label = new Label(text: ""loc:foo label"");
            TextField textField = new TextField(label: ""loc:foo textfield"");
            IntegrationTestWindow window = IntegrationTestWindow.Create(label, textField);
            TranslateElementTree(label);
            TranslateElementTree(textField);

            result.Add(label.text);
            result.Add(textField.label);

            SelectLocale(CultureInfo.GetCultureInfo(""ja""));

            result.Add(label.text);
            result.Add(textField.label);

            window.Close();
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            Assert.That(
                File.Exists(Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po")),
                Is.True);
            _ = new GetTextCatalogBuilder()
                .ForPoFile("Gen/ja-integrationtest.po", locale(), d => d
                    .AddEntry(e => e
                        .ConfigureId("foo label")
                        .ConfigureValue("ja:foo label"))
                    .AddEntry(e => e
                        .ConfigureId("foo textfield")
                        .ConfigureValue("ja:foo textfield")))
                .WriteToDisk(TestAssemblyRootFolder);

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "foo label",
                "foo textfield",
                "ja:foo label",
                "ja:foo textfield",
            }));
        }

        [UnityTest]
        public IEnumerator EZLocalization_AutoRetranslates_WhenPoFileChanged()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());
            GenerateEmptyProxyType(languageAttribute);

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            GenerateTestAction(
                localizationFieldDeclaration: string.Empty,
                code: $@"
            Label label = new Label(text: ""loc:foo label"");
            TextField textField = new TextField(label: ""loc:foo textfield"");
            IntegrationTestWindow window = IntegrationTestWindow.Create(label, textField);
            TranslateElementTree(label);
            TranslateElementTree(textField);

            SelectLocale(CultureInfo.GetCultureInfo(""ja""));

            result.Add(label.text);
            result.Add(textField.label);

            //will assume the underlying file already exists, since our other tests verify that much
            _ = new GetTextCatalogBuilder()
                .ForPoFile(
                    ""Gen/ja-integrationtest.po"",
                    new Locale(
                        CultureInfo.GetCultureInfo(""ja""),
                        new PluralRules(other: ""@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"")),
                    d => d
                        .AddEntry(e => e
                            .ConfigureId(""foo label"")
                            .ConfigureValue(""ja:foo label""))
                        .AddEntry(e => e
                            .ConfigureId(""foo textfield"")
                            .ConfigureValue(""ja:foo textfield"")))
                    .WriteToDisk(""{TestAssemblyRootFolder}"");

            //need to wait for reload and refresh to happen
            //the sleep is to make sure the asynchronous FileSystemWatcher adds its delaycall first,
            //to make testing more deterministic
            System.Threading.Thread.Sleep(1000);
            EditorApplication.delayCall += () =>
            {{
                result.Add(label.text);
                result.Add(textField.label);
                window.Close();
            }};
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            //since the test action does delay calls waiting for reloads to happen, we need to wait all the same
            yield return null;

            Assert.That(result, Is.EqualTo(new[]
            {
                "foo label",
                "foo textfield",
                "ja:foo label",
                "ja:foo textfield",
            }));
        }

        [UnityTest]
        public IEnumerator EZLocalization_AutoRetranslatesMenus()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());
            GenerateEmptyProxyType(languageAttribute);

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();


            File.WriteAllText(Path.Combine(TestAssemblyRootFolder, "Gen", "GenerateMenus.cs"), @"
namespace EZUtils.Localization.Tests.Integration
{
    using EZUtils.Localization;
    using System.Globalization;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;
    using static Localization;

    public static class CreateMenus
    {
        [InitializeOnLoadMethod]
        private static void UnityInitialize() => AddMenu(""IntegrationTest/Menu"", priority: 0, () => { });
    }
}");

            GenerateTestAction(
                localizationFieldDeclaration: string.Empty,
                code: @"
            string GetResult()
            {
                string[] subItems = (string[])typeof(Menu).GetMethod(
                    ""ExtractSubmenus"", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { ""IntegrationTest"" });
                return string.Join("", "", subItems);
            }

            result.Add(GetResult());

            SelectLocale(CultureInfo.GetCultureInfo(""ja""));

            result.Add(GetResult());

            SelectLocale(Locale.English);

            result.Add(GetResult());
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            Assert.That(File.Exists(poFilePath), Is.True);
            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath, locale(), d => d
                    .OverwriteEntry(e => e
                        .ConfigureId("IntegrationTest/Menu")
                        .ConfigureValue("IntegrationTest/ja:Menu")))
                .WriteToDisk();
            //the initialization that adds menus causes the first load of the catalog to happen before we write our changes
            yield return WaitForCatalogReload();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "IntegrationTest/Menu",
                "IntegrationTest/ja:Menu",
                "IntegrationTest/Menu",
            }));
        }

        [UnityTest]
        public IEnumerator SetLocale_SynchronizesAcrossMultipleInstance()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());
            GenerateTestAction(
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute),
                code: $@"
            EZLocalization loc2 = EZLocalization.ForCatalogUnder(""{TestAssemblyRootFolder}Gen"", ""EZLocalizationExtractorIntegrationTests"");
            result.Add(loc.T(""foo""));
            result.Add(loc2.T(""foo""));

            loc.SelectLocale(CultureInfo.GetCultureInfo(""ja""));

            result.Add(loc.T(""foo""));
            result.Add(loc2.T(""foo""));
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            Assert.That(File.Exists(poFilePath), Is.True);
            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath, locale(), d => d
                    .OverwriteEntry(e => e
                        .ConfigureId("foo")
                        .ConfigureValue("ja:foo")))
                .WriteToDisk();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "foo",
                "foo",
                "ja:foo",
                "ja:foo",
            }));
        }

        [UnityTest]
        public IEnumerator SetLocale_DoesNotSynchronize_IfKeysAreDifferent()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());
            GenerateTestAction(
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute),
                code: $@"
            EZLocalization loc2 = EZLocalization.ForCatalogUnder(""{TestAssemblyRootFolder}Gen"", ""foo"");
            result.Add(loc.T(""foo""));
            result.Add(loc2.T(""foo""));

            loc.SelectLocale(CultureInfo.GetCultureInfo(""ja""));

            result.Add(loc.T(""foo""));
            result.Add(loc2.T(""foo""));
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            Assert.That(File.Exists(poFilePath), Is.True);
            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath, locale(), d => d
                    .OverwriteEntry(e => e
                        .ConfigureId("foo")
                        .ConfigureValue("ja:foo")))
                .WriteToDisk();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "foo",
                "foo",
                "ja:foo",
                "foo",
            }));
        }

        [UnityTest]
        public IEnumerator SetLocale_DoesNotSynchronize_IfCatalogDoesNotSupportLocale()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());
            GenerateTestAction(
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute),
                code: $@"
            EZLocalization loc2 = EZLocalization.ForCatalogUnder(""{TestAssemblyRootFolder}Gen/othercatalog"", ""EZLocalizationExtractorIntegrationTests"");
            result.Add(loc.T(""foo""));
            result.Add(loc2.T(""foo""));

            //loc should be ja, loc2 should remain as native en
            loc.SelectLocale(CultureInfo.GetCultureInfo(""ja""));

            result.Add(loc.T(""foo""));
            result.Add(loc2.T(""foo""));

            //loc2 should be ko, loc should remain as previously set ja
            loc2.SelectLocale(CultureInfo.GetCultureInfo(""ko""));

            result.Add(loc.T(""foo""));
            result.Add(loc2.T(""foo""));

            //both support en, so both sync to it
            loc.SelectLocale(Locale.English);

            result.Add(loc.T(""foo""));
            result.Add(loc2.T(""foo""));
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            Assert.That(File.Exists(poFilePath), Is.True);
            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath, locale(), d => d
                    .OverwriteEntry(e => e
                        .ConfigureId("foo")
                        .ConfigureValue("ja:foo")))
                .ForPoFile(
                    Path.Combine(TestAssemblyRootFolder, "Gen", "othercatalog", "ko-integrationtest.po"),
                    new Locale(
                        CultureInfo.GetCultureInfo("ko"),
                        new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")),
                    d => d
                        .AddEntry(e => e
                            .ConfigureId("foo")
                            .ConfigureValue("ko:foo")))
                .WriteToDisk();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "foo",
                "foo",

                "ja:foo",
                "foo",

                "ja:foo",
                "ko:foo",

                "foo",
                "foo"
            }));
        }

        [UnityTest]
        public IEnumerator EZLocalization_UsesStoredSynchronizedLocale_WhenLoaded()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());
            GenerateTestAction(
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute),
                code: $@"
            loc.SelectLocale(CultureInfo.GetCultureInfo(""ja""));
            //this loc call needs to be here for extraction to happen
            result.Add(loc.T(""foo""));

            EZLocalization loc2 = EZLocalization.ForCatalogUnder(""{TestAssemblyRootFolder}Gen"", ""EZLocalizationExtractorIntegrationTests"");
            result.Add(loc2.T(""foo""));

            loc.SelectLocale(Locale.English);

            loc2 = EZLocalization.ForCatalogUnder(""{TestAssemblyRootFolder}Gen"", ""EZLocalizationExtractorIntegrationTests"");
            result.Add(loc2.T(""foo""));
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            Assert.That(File.Exists(poFilePath), Is.True);
            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath, locale(), d => d
                    .OverwriteEntry(e => e
                        .ConfigureId("foo")
                        .ConfigureValue("ja:foo")))
                .WriteToDisk();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "ja:foo",
                "ja:foo",
                "foo"
            }));
        }

        [UnityTest]
        public IEnumerator AutomatedExtraction_PrunesEntries_WhenRemovedFromCode()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute() => GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());

            GenerateTestAction(
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute()),
                code: $@"
            result.Add(loc.T(""foo""));
            result.Add(loc.T(""bar""));
            ");
            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            GenerateTestAction(
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute()),
                code: $@"
            //result.Add(loc.T(""foo""));
            result.Add(loc.T(""bar""));
            ");
            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            GetTextDocument document = GetTextDocument.LoadFrom(poFilePath);

            Assert.That(document.Entries.Count, Is.EqualTo(3));
            Assert.That(document.Entries[1].Id, Is.EqualTo("foo"));
            Assert.That(document.Entries[1].IsObsolete, Is.True);
            Assert.That(document.Entries[2].Id, Is.EqualTo("bar"));
            Assert.That(document.Entries[2].IsObsolete, Is.False);

            GenerateTestAction(
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute()),
                code: $@"
            //result.Add(loc.T(""foo""));
            //result.Add(loc.T(""bar""));
            ");
            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            document = GetTextDocument.LoadFrom(poFilePath);

            Assert.That(document.Entries.Count, Is.EqualTo(3));
            Assert.That(document.Entries[1].Id, Is.EqualTo("foo"));
            Assert.That(document.Entries[1].IsObsolete, Is.True);
            Assert.That(document.Entries[2].Id, Is.EqualTo("bar"));
            Assert.That(document.Entries[2].IsObsolete, Is.True);
        }

        [UnityTest]
        public IEnumerator AutomatedExtraction_DoesNotPruneEntries_WhenRemovedFromCodeButMarkedAsKeep()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());
            GenerateEmptyProxyType(languageAttribute);

            string poFilePath() => Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath(), locale(), d => d
                    .AddEntry(e => e
                        .AddEmptyLine()
                        .AddComment(", keep")
                        .ConfigureId("foo")
                        .ConfigureValue("ja:foo")))
                .WriteToDisk();

            //we dont have action code, since we don't need it
            //a domain reload will happen from the generated proxy type
            AssetDatabase.Refresh();
            //is presumably because unity cant read our po files
            //this hits here and not other tests presumably because error log detection breaks after domain reload
            //and all other cases don't write a po file until generated after domain reload
            LogAssert.Expect(UnityEngine.LogType.Error, "File couldn't be read");
            yield return new WaitForDomainReload();

            GetTextDocument document = GetTextDocument.LoadFrom(poFilePath());

            Assert.That(document.Entries.Count, Is.EqualTo(2));
            Assert.That(document.Entries[1].Id, Is.EqualTo("foo"));
            Assert.That(document.Entries[1].IsObsolete, Is.False);
        }

        [UnityTest]
        public IEnumerator AutoExtraction_ExtractsUxml_WhenNoGenerateLanguageElementFound()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());

            File.WriteAllText(Path.Combine(TestAssemblyRootFolder, "Gen", "UI.uxml"),
@"<?xml version=""1.0"" encoding=""utf-8""?>
<UXML
  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
  xmlns=""UnityEngine.UIElements""
  xmlns:editor=""UnityEditor.UIElements""
  xsi:schemaLocation=""
    UnityEngine.UIElements ../../UIElementsSchema/UnityEngine.UIElements.fixed.xsd
    UnityEditor.UIElements ../../UIElementsSchema/UnityEditor.UIElements.xsd"">
  <VisualElement>
    <Label text=""loc:foo label"" />
    <TextField label=""loc:bar textfield"" />
  </VisualElement>
</UXML>");

            GenerateTestAction(
                localizationFieldDeclaration: GenerateLocalizationFieldDeclaration(languageAttribute),
                code: $@"
            IntegrationTestWindow window = IntegrationTestWindow.Create();
            VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(""{TestAssemblyRootFolder}/Gen/UI.uxml"");
            visualTreeAsset.CloneTree(window.rootVisualElement);
            loc.TranslateElementTree(window.rootVisualElement);

            result.Add(window.rootVisualElement.Q<Label>().text);
            result.Add(window.rootVisualElement.Q<TextField>().label);

            loc.SelectLocale(CultureInfo.GetCultureInfo(""ja""));

            result.Add(window.rootVisualElement.Q<Label>().text);
            result.Add(window.rootVisualElement.Q<TextField>().label);

            window.Close();
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            Assert.That(File.Exists(poFilePath), Is.True);
            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath, locale(), d => d
                    .OverwriteEntry(e => e
                        .ConfigureId("foo label")
                        .ConfigureValue("ja:foo label"))
                    .OverwriteEntry(e => e
                        .ConfigureId("bar textfield")
                        .ConfigureValue("ja:bar textfield")))
                .WriteToDisk();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "foo label",
                "bar textfield",
                "ja:foo label",
                "ja:bar textfield",
            }));
        }

        [UnityTest]
        public IEnumerator AutoExtraction_ExtractsUxml_WhenGenerateLanguageElementFound()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());

            File.WriteAllText(Path.Combine(TestAssemblyRootFolder, "Gen", "UI.uxml"),
@"<?xml version=""1.0"" encoding=""utf-8""?>
<UXML
  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
  xmlns=""UnityEngine.UIElements""
  xmlns:editor=""UnityEditor.UIElements""
  xmlns:loc=""EZUtils.Localization.UIElements"">
  <VisualElement>
    <loc:GenerateLanguage poFilePath=""Gen/ja-integrationtest.po"" cultureInfoCode=""ja"" other=""@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"" />
    <Label text=""loc:foo label"" />
    <TextField label=""loc:bar textfield"" />
  </VisualElement>
</UXML>");

            GenerateTestAction(
                code: $@"
            //making a local so that we can verify we're not pulling from a [GenerateLanguage]
            EZLocalization loc = EZLocalization.ForCatalogUnder(""{TestAssemblyRootFolder}Gen"", ""EZLocalizationExtractorIntegrationTests"");

            IntegrationTestWindow window = IntegrationTestWindow.Create();
            VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(""{TestAssemblyRootFolder}/Gen/UI.uxml"");
            visualTreeAsset.CloneTree(window.rootVisualElement);
            loc.TranslateElementTree(window.rootVisualElement);

            result.Add(window.rootVisualElement.Q<Label>().text);
            result.Add(window.rootVisualElement.Q<TextField>().label);

            loc.SelectLocale(CultureInfo.GetCultureInfo(""ja""));

            result.Add(window.rootVisualElement.Q<Label>().text);
            result.Add(window.rootVisualElement.Q<TextField>().label);

            window.Close();
            ");

            AssetDatabase.Refresh();
            yield return new WaitForDomainReload();

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            Assert.That(File.Exists(poFilePath), Is.True);
            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath, locale(), d => d
                    .OverwriteEntry(e => e
                        .ConfigureId("foo label")
                        .ConfigureValue("ja:foo label"))
                    .OverwriteEntry(e => e
                        .ConfigureId("bar textfield")
                        .ConfigureValue("ja:bar textfield")))
                .WriteToDisk();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            Assert.That(result, Is.EqualTo(new[]
            {
                "foo label",
                "bar textfield",
                "ja:foo label",
                "ja:bar textfield",
            }));
        }

        [UnityTest]
        public IEnumerator EZLocalization_DoesNotThrow_WhenPoFilesAlreadyInvalid()
        {
            GenerateAssemblyAttribute();
            GenerateTestAction(
                code: $@"
            //making a local so that extraction does not produce usable catalogs
            EZLocalization loc = EZLocalization.ForCatalogUnder(""{TestAssemblyRootFolder}Gen"", ""EZLocalizationExtractorIntegrationTests"");
            result.Add(loc.T(""foo""));
            ");

            //we have no other good way to produce bad files
            //invalid due to lack of header
            File.WriteAllText(Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po"), @"
msgid ""foo""
msgstr ""bar""
");

            AssetDatabase.Refresh();
            //is presumably because unity cant read our po files
            //this hits here and not other tests presumably because error log detection breaks after domain reload
            //and all other cases don't write a po file until generated after domain reload
            LogAssert.Expect(UnityEngine.LogType.Error, "File couldn't be read");
            yield return new WaitForDomainReload();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("GetTextParseException"));

            Assert.That(result, Is.EqualTo(new[]
            {
                "foo"
            }));
        }

        [UnityTest]
        public IEnumerator EZLocalization_DoesNotThrow_WhenPoFilesBecomeInvalid()
        {
            GenerateAssemblyAttribute();
            Locale locale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));
            string languageAttribute = GenerateLocalizationAttribute("Gen/ja-integrationtest.po", locale());

            string poFilePath = Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            GenerateTestAction(
                code: $@"
            //making a local so that extraction does not produce usable catalogs
            EZLocalization loc = EZLocalization.ForCatalogUnder(""{TestAssemblyRootFolder}Gen"", ""EZLocalizationExtractorIntegrationTests"");

            loc.SelectLocale(CultureInfo.GetCultureInfo(""ja""));
            result.Add(loc.T(""foo""));

            System.IO.File.WriteAllText(""{poFilePath.Replace("\\", "\\\\")}"", @""
                msgid """"foo""""
                msgstr """"bar""""
                "");

            //need to wait for reload and refresh to happen
            //the sleep is to make sure the asynchronous FileSystemWatcher adds its delaycall first,
            //to make testing more deterministic
            System.Threading.Thread.Sleep(1000);
            EditorApplication.delayCall += () =>
            {{
                result.Add(loc.T(""foo""));
            }};
            ");

            _ = new GetTextCatalogBuilder()
                .ForPoFile(poFilePath, locale(), d => d
                    .AddEntry(e => e
                        .ConfigureId("foo")
                        .ConfigureValue("ja:foo")))
                .WriteToDisk();

            AssetDatabase.Refresh();
            //is presumably because unity cant read our po files
            //this hits here and not other tests presumably because error log detection breaks after domain reload
            //and all other cases don't write a po file until generated after domain reload
            LogAssert.Expect(UnityEngine.LogType.Error, "File couldn't be read");
            yield return new WaitForDomainReload();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("GetTextParseException"));

            //since the test action does delay calls waiting for reloads to happen, we need to wait all the same
            yield return null;

            Assert.That(result, Is.EqualTo(new[]
            {
                "ja:foo",
                "ja:foo"
            }));
        }

        //also covers the cases of working the first time around, of course
        //TODO: would also be nice to simulate a click, but it's not exactly worth it atm
        [UnityTest]
        public IEnumerator ToolbarAddLocaleSelector_UpdatesMenuWithLocales_WhenNewLocalesAddedMidway()
        {
            string jaPoFilePath() => Path.Combine(TestAssemblyRootFolder, "Gen", "ja-integrationtest.po");
            string koPoFilePath() => Path.Combine(TestAssemblyRootFolder, "Gen", "ko-integrationtest.po");
            Locale jaLocale() => new Locale(
                CultureInfo.GetCultureInfo("ja"),
                new PluralRules(other: "@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"));

            _ = new GetTextCatalogBuilder()
                .ForPoFile(jaPoFilePath(), jaLocale(), d => d
                    .AddEntry(e => e
                        .ConfigureId("foo")
                        .ConfigureValue("ja:foo")))
                .WriteToDisk();
            GenerateTestAction(
                code: $@"
            EZLocalization loc = EZLocalization.ForCatalogUnder(""{TestAssemblyRootFolder}Gen"", ""{LocaleSynchronizationKey}"");

            Toolbar toolbar = new Toolbar();
            IntegrationTestWindow window = IntegrationTestWindow.Create(toolbar);
            //NOTE: pretty crucial here is that we dont use loc before AddLocaleSelector
            //because -- hint -- we necessarily delay initialize localization synchronization stuff
            //we expect 1 for the native language, at least
            EZUtils.Localization.UIElements.LocalizationUIExtensions.AddLocaleSelector(toolbar, ""{LocaleSynchronizationKey}"", Locale.English);

            result.Add(toolbar.Q<EZUtils.UIElements.ToolbarMenu>().menu.MenuItems().Count.ToString());

            //we're okay with lack of T calls causing the dropdown to not contain the language
            //but if you're adding the control to change languages and not doing any localization, what's the point?
            _ = loc.T(""foo"");
            result.Add(toolbar.Q<EZUtils.UIElements.ToolbarMenu>().menu.MenuItems().Count.ToString());

            _ = new GetTextCatalogBuilder()
                .ForPoFile(
                    ""{koPoFilePath().Replace("\\", "\\\\")}"",
                    new Locale(
                        CultureInfo.GetCultureInfo(""ko""),
                        new PluralRules(other: ""@integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …"")) ,
                    d => d
                    .AddEntry(e => e
                        .ConfigureId(""foo"")
                        .ConfigureValue(""ko:foo"")))
                .WriteToDisk();

            //need to wait for reload and refresh to happen
            //the sleep is to make sure the asynchronous FileSystemWatcher adds its delaycall first,
            //to make testing more deterministic
            System.Threading.Thread.Sleep(1000);
            EditorApplication.delayCall += () =>
            {{
                result.Add(toolbar.Q<EZUtils.UIElements.ToolbarMenu>().menu.MenuItems().Count.ToString());
                window.Close();
            }};
            ");

            AssetDatabase.Refresh();
            //is presumably because unity cant read our po files
            //this hits here and not other tests presumably because error log detection breaks after domain reload
            //and all other cases don't write a po file until generated after domain reload
            LogAssert.Expect(UnityEngine.LogType.Error, "File couldn't be read");
            yield return new WaitForDomainReload();

            IReadOnlyList<string> result = new IntegrationTestAction().Execute();

            //since the test action does delay calls waiting for reloads to happen, we need to wait all the same
            yield return null;

            Assert.That(result, Is.EqualTo(new[]
            {
                "1", //en
                "2", //plus jp
                "3", //plus ko
            }));
        }

        //this only needs to be done for cases where EZLocalization is used before we overwrite our po files
        private static object WaitForCatalogReload()
        {
            //far from ideal, as it's a potential source of flakiness
            //but at least it's an integration test
            System.Threading.Thread.Sleep(1000);
            return null; //to be yield returned
        }

        private static void GenerateAssemblyAttribute()
        => File.WriteAllText(
            Path.Combine(TestAssemblyRootFolder, "Gen", "Assembly.cs"),
            "[assembly: EZUtils.Localization.LocalizedAssembly]");

        private static void GenerateEmptyProxyType(params string[] languageAttributes)
            => File.WriteAllText(Path.Combine(TestAssemblyRootFolder, "Gen", "Localization.cs"), $@"
namespace EZUtils.Localization.Tests.Integration
{{
    using EZUtils.Localization;

    [LocalizationProxy]
    {string.Join("\n    ", languageAttributes)}
    public static partial class Localization
    {{
        //for testing, we could let the generator generate this, then replace the path
        //but this is just so much easier
        {GenerateLocalizationFieldDeclaration(languageAttributes)}
    }}
}}");
        private static void GenerateTestAction(string code)
            => GenerateTestAction(string.Empty, code);
        private static void GenerateTestAction(string localizationFieldDeclaration, string code)
            => File.WriteAllText(Path.Combine(TestAssemblyRootFolder, "Gen", "IntegrationTestAction.cs"), $@"
namespace EZUtils.Localization.Tests.Integration
{{
    using EZUtils.Localization;
    using System.Globalization;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;
    using static Localization;

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
                _ = sb.Append(languageAttribute).AppendLine();
            }
            _ = sb.Append($@"private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder(""{TestAssemblyRootFolder}Gen"", ""{LocaleSynchronizationKey}"");");
            return sb.ToString();
        }
    }
}

//a convenient place to write test action code with intellisense
//just parts of code into GenerateTestAction call
//namespace EZUtils.Localization.Tests.Integration
//{
//    using System.Collections.Generic;
//    using System.Globalization;
//    using EZUtils.Localization;
//    using NUnit.Framework.Interfaces;
//    using UnityEditor;
//    using UnityEditor.UIElements;
//    using UnityEngine.UIElements;
//    using static Localization;

//    public partial class IntegrationTestAction
//    {
//        [GenerateLanguage("ja", "ja-inttest.po", Other = " @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")]
//        [GenerateLanguage("ko", "ko-inttest.po", Other = " @integer 0~15, 100, 1000, 10000, 100000, 1000000, … @decimal 0.0~1.5, 10.0, 100.0, 1000.0, 10000.0, 100000.0, 1000000.0, …")]
//        private static readonly EZLocalization loc = EZLocalization.ForCatalogUnder("Packages/com.timiz0r.ezutils.localization.tests/IntegrationTestAssembly/Gen", "EZLocalizationExtractorIntegrationTests");

//        private IReadOnlyList<string> ExecuteImpl()
//        {
//            List<string> result = new List<string>();

//            //
//            loc.SelectLocale(CultureInfo.GetCultureInfo("ja"));

//            IntegrationTestWindow window = IntegrationTestWindow.Create();
//            VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("/UI.uxml");
//            visualTreeAsset.CloneTree(window.rootVisualElement);
//            loc.TranslateElementTree(window.rootVisualElement);

//            result.Add(window.rootVisualElement.Q<Label>().text);
//            result.Add(window.rootVisualElement.Q<FloatField>().text);

//            window.Close();

//            return result;
//        }
//    }
//}
