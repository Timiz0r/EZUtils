namespace EZUtils.MMDAvatarTools.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using EZUtils.Localization;
    using NUnit.Framework;

    /*
     * TODO analyzers
     * add instructions for fixing issues
     * double check that null renderers fail tests
     */
    //technically testing is a bit insufficient because we dont test sub state machines and only layers' state machines
    public class GetTextDocumentParsingTests
    {
        private static readonly string genericHeader = @"
msgid """"
msgstr ""Language: ja\n""";
        [Test]
        public void Throws_WhenFirstEntryNotHeader()
        {
            string document = @"
msgid ""foo""
msgstr ""bar""";

            Assert.That(() => GetTextDocument.Parse(document), Throws.InvalidOperationException);
        }
        [Test]
        public void Throws_WhenHeaderEntryIncludesContext()
        {
            string document = @"
msgctxt """"
msgid """"
msgstr ""bar""";

            Assert.That(() => GetTextDocument.Parse(document), Throws.InvalidOperationException);
        }

        [Test]
        public void Throws_IfDuplicateEntryFound()
        {
            string document = genericHeader + @"
msgid ""foo""
msgstr ""bar""

msgid ""foo""
msgstr ""baz""";

            Assert.That(() => GetTextDocument.Parse(document), Throws.InvalidOperationException);

            document = genericHeader + @"
msgctxt ""apple""
msgid ""foo""
msgstr ""bar""

msgctxt ""apple""
msgid ""foo""
msgstr ""baz""";

            Assert.That(() => GetTextDocument.Parse(document), Throws.InvalidOperationException);
        }

        [Test]
        public void ParsesTwoEntries_WhenSameIdButDifferentContext()
        {
            string document = genericHeader + @"
msgctxt ""apple""
msgid ""foo""
msgstr ""bar""

msgctxt ""orange""
msgid ""foo""
msgstr ""baz""";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            Assert.That(getTextDocument.Entries, Has.Count.EqualTo(3));
            _ = AssertHasEntry(getTextDocument, "apple", "foo");
            _ = AssertHasEntry(getTextDocument, "orange", "foo");
        }

        [Test]
        public void ParsesTwoEntries_WhenSameContextButDifferentId()
        {
            string document = genericHeader + @"
msgctxt ""apple""
msgid ""foo""
msgstr ""bar""

msgctxt ""apple""
msgid ""bar""
msgstr ""baz""";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            Assert.That(getTextDocument.Entries, Has.Count.EqualTo(3));
            _ = AssertHasEntry(getTextDocument, "apple", "foo");
            _ = AssertHasEntry(getTextDocument, "apple", "bar");
        }

        private static GetTextEntry AssertHasEntry(GetTextDocument getTextDocument, string id)
            => AssertHasEntry(getTextDocument, context: null, id: id);
        private static GetTextEntry AssertHasEntry(GetTextDocument getTextDocument, string context, string id)
        {
            GetTextEntry[] entries = getTextDocument.Entries.Where(e => e.Context == context && e.Id == id).ToArray();
            Assert.That(entries, Has.Length.EqualTo(1));

            return entries[0];
        }
    }
}
