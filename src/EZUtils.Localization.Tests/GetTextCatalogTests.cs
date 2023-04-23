namespace EZUtils.Localization.Tests
{
    using System.Globalization;
    using NUnit.Framework;

    public class GetTextCatalogTests
    {
        private static readonly Locale jp = new Locale(CultureInfo.GetCultureInfo("ja"), new PluralRules(other: ""), useSpecialZero: true);

        [Test]
        public void T_ReturnsId_WhenNativeLocaleSelected()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("foo").ConfigureValue("bar")))
                .GetCatalog(Locale.English);

            catalog.SelectLocale(Locale.English);

            Assert.That(catalog.T("foo"), Is.EqualTo("foo"));
        }

        [Test]
        public void T_ReturnsTranslatedValue_WhenOtherLocaleSelected()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("foo").ConfigureValue("bar")))
                .GetCatalog(Locale.English);

            catalog.SelectLocale(jp);

            Assert.That(catalog.T("foo"), Is.EqualTo("bar"));
        }

        [Test]
        public void T_ReturnsIdFromFormattedString_WhenNativeLocaleSelected()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("foo {0}").ConfigureValue("bar {0}")))
                .GetCatalog(Locale.English);

            catalog.SelectLocale(Locale.English);

            Assert.That(catalog.T($"foo {1}"), Is.EqualTo("foo 1"));
        }

        [Test]
        public void T_ReturnsTranslatedValueFromFormattedString_WhenOtherLocaleSelected()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("foo {0}").ConfigureValue("bar {0}")))
                .GetCatalog(Locale.English);

            catalog.SelectLocale(jp);

            Assert.That(catalog.T($"foo {1}"), Is.EqualTo("bar 1"));
        }

        [Test]
        public void SelectLocaleOrNative_SelectsSupportedLocale()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("foo").ConfigureValue("bar")))
                .GetCatalog(Locale.English);

            _ = catalog.SelectLocaleOrNative(jp);

            Assert.That(catalog.T("foo"), Is.EqualTo("bar"));
        }

        [Test]
        public void SelectLocaleOrNative_SelectsNativeLocale_WhenPassedUnsupportedLocale()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("foo").ConfigureValue("bar")))
                .GetCatalog(Locale.English);

            _ = catalog.SelectLocaleOrNative(new Locale(CultureInfo.GetCultureInfo("ko")));

            Assert.That(catalog.T("foo"), Is.EqualTo("foo"));
        }

        [Test]
        public void T_ReturnsNativePluralOne_WhenOnePassed()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("{0} foo")
                        .ConfigureAsPlural("{0} foos")
                        .ConfigureAdditionalPluralValue("{0} foo")
                        .ConfigureAdditionalPluralValue("fooがありません")))
                .GetCatalog(Locale.English);

            catalog.SelectLocale(Locale.English);

            decimal count = 1;
            Assert.That(catalog.T($"{count} foo", count, $"{count} foos"), Is.EqualTo("1 foo"));
        }

        [Test]
        public void T_ReturnsNativePluralOther_WhenTwoPassed()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("{0} foo")
                        .ConfigureAsPlural("{0} foos")
                        .ConfigureAdditionalPluralValue("{0} foo")
                        .ConfigureAdditionalPluralValue("fooがありません")))
                .GetCatalog(Locale.English);

            catalog.SelectLocale(Locale.English);

            decimal count = 2;
            Assert.That(catalog.T($"{count} foo", count, $"{count} foos"), Is.EqualTo("2 foos"));
        }

        [Test]
        public void T_ReturnsOtherLocalePluralOne_WhenOnePassed()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("{0} foo")
                        .ConfigureAsPlural("{0} foos")
                        .ConfigureAdditionalPluralValue("{0} foo")
                        .ConfigureAdditionalPluralValue("fooがありません")))
                .GetCatalog(Locale.English);

            catalog.SelectLocale(jp);

            decimal count = 1;
            Assert.That(catalog.T($"{count} foo", count, $"{count} foos"), Is.EqualTo("1 foo"));
        }

        [Test]
        public void T_ReturnsOtherLocalePluralOther_WhenTwoPassed()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("{0} foo")
                        .ConfigureAsPlural("{0} foos")
                        .ConfigureAdditionalPluralValue("{0} foo")
                        .ConfigureAdditionalPluralValue("fooがありません")))
                .GetCatalog(Locale.English);

            catalog.SelectLocale(jp);

            decimal count = 2;
            Assert.That(catalog.T($"{count} foo", count, $"{count} foos"), Is.EqualTo("2 foo"));
        }

        [Test]
        public void T_ReturnsOtherLocalePluralSpecialZero_WhenZeroPassed()
        {
            GetTextCatalogBuilder catalogBuilder = new GetTextCatalogBuilder();
            GetTextCatalog catalog = catalogBuilder
                .ForPoFile("unittest-ja.po", jp, d => d
                    .AddEntry(e => e
                        .ConfigureId("{0} foo")
                        .ConfigureAsPlural("{0} foos")
                        .ConfigureAdditionalPluralValue("{0} foo")
                        .ConfigureAdditionalPluralValue("fooがありません")))
                .GetCatalog(Locale.English);

            catalog.SelectLocale(jp);

            decimal count = 0;
            Assert.That(catalog.T($"{count} foo", count, $"{count} foos"), Is.EqualTo("fooがありません"));
        }
    }
}
