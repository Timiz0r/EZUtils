namespace EZUtils.Localization.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using EZUtils.Localization;
    using NUnit.Framework;

    public class GetTextDocumentParsingTests
    {
        private static readonly string genericHeader = @"
msgid """"
msgstr ""Language: ja\n""
";

        [Test]
        public void Throws_WhenFirstEntryNotHeader()
        {
            string document = @"
msgid ""foo""
msgstr ""bar""";

            Assert.That(() => GetTextDocument.Parse(document), Throws.InvalidOperationException);
        }

        [Test]
        public void ReturnsRightHeader_WhenJustLanguage()
        {
            string document = @"
msgid """"
msgstr ""Language: az_AZ@latin\n""";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);
            GetTextHeader header = getTextDocument.Header;

            Assert.That(header.Locale.CultureInfo.Name, Is.EqualTo("az-Latn-AZ"));
            Assert.That(header.Locale.PluralRules, Is.EqualTo(new PluralRules()));
            Assert.That(header.Locale.UseSpecialZero, Is.False);
        }

        [Test]
        public void ReturnsRightHeader_WhenPluralRulesPresent()
        {
            PluralRules expectedPluralRules = new PluralRules(
                zero: "n = 0 @integer 0 @decimal 0.0, 0.00, 0.000, 0.0000",
                one: "n = 1 @integer 1 @decimal 1.0, 1.00, 1.000, 1.0000",
                two: "n % 100 = 2,22,42,62,82 or n % 1000 = 0 and n % 100000 = 1000..20000,40000,60000,80000 or n != 0 and n % 1000000 = 100000 @integer 2, 22, 42, 62, 82, 102, 122, 142, 1000, 10000, 100000, … @decimal 2.0, 22.0, 42.0, 62.0, 82.0, 102.0, 122.0, 142.0, 1000.0, 10000.0, 100000.0, …",
                few: "n % 100 = 3,23,43,63,83 @integer 3, 23, 43, 63, 83, 103, 123, 143, 1003, … @decimal 3.0, 23.0, 43.0, 63.0, 83.0, 103.0, 123.0, 143.0, 1003.0, …",
                many: "n != 1 and n % 100 = 1,21,41,61,81 @integer 21, 41, 61, 81, 101, 121, 141, 161, 1001, … @decimal 21.0, 41.0, 61.0, 81.0, 101.0, 121.0, 141.0, 161.0, 1001.0, …",
                other: " @integer 4~19, 100, 1004, 1000000, … @decimal 0.1~0.9, 1.1~1.7, 10.0, 100.0, 1000.1, 1000000.0, …");
            string document = $@"
msgid """"
msgstr ""Language: az_AZ@latin\n""
""X-PluralRules-Zero: {expectedPluralRules.Zero}\n""
""X-PluralRules-One: {expectedPluralRules.One}\n""
""X-PluralRules-Two: {expectedPluralRules.Two}\n""
""X-PluralRules-Few: {expectedPluralRules.Few}\n""
""X-PluralRules-Many: {expectedPluralRules.Many}\n""
""X-PluralRules-Other: {expectedPluralRules.Other}\n""
""X-PluralRules-SpecialZero: \n""
";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);
            GetTextHeader header = getTextDocument.Header;

            Assert.That(header.Locale.CultureInfo.Name, Is.EqualTo("az-Latn-AZ"));
            Assert.That(header.Locale.PluralRules, Is.EqualTo(expectedPluralRules));
            Assert.That(header.Locale.UseSpecialZero, Is.True);
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
        public void Throws_IfContextFollowedByContext()
        {
            string document = genericHeader + @"
msgctxt ""apple""

msgctxt ""orange""
msgid ""foo""
msgstr ""bar""";

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

            AssertNonHeaderEntries(getTextDocument, 2);
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

            AssertNonHeaderEntries(getTextDocument, 2);
            _ = AssertHasEntry(getTextDocument, "apple", "foo");
            _ = AssertHasEntry(getTextDocument, "apple", "bar");
        }

        [Test]
        public void ParsesTwoEntries_WhenOnlyFirstHasContext()
        {
            string document = genericHeader + @"
msgctxt ""apple""
msgid ""foo""
msgstr ""bar""

msgid ""foo""
msgstr ""baz""";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            AssertNonHeaderEntries(getTextDocument, 2);
            _ = AssertHasEntry(getTextDocument, "apple", "foo");
            _ = AssertHasEntry(getTextDocument, "foo");
        }

        [Test]
        public void ParsesTwoEntries_WhenOnlySecondHasContext()
        {
            string document = genericHeader + @"
msgid ""foo""
msgstr ""bar""

msgctxt ""apple""
msgid ""foo""
msgstr ""baz""";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            AssertNonHeaderEntries(getTextDocument, 2);
            _ = AssertHasEntry(getTextDocument, "foo");
            _ = AssertHasEntry(getTextDocument, "apple", "foo");
        }

        [Test]
        public void ParsesComments_WhenPlacedInAllSortsOfPlaces()
        {
            string document = genericHeader + @"
# comment at start
msgid ""foo"" #comment inline to msgid
#comment between keywords
msgstr ""bar""
#comment at end";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);
            IReadOnlyList<GetTextLine> lines = getTextDocument.Entries[1].Lines;

            AssertNonHeaderEntries(getTextDocument, 1);
            Assert.That(lines[0].IsWhiteSpace, Is.True); //just becaus it's not 100% obvious reading code
            Assert.That(lines[1].Comment, Is.EqualTo(" comment at start"));
            Assert.That(lines[2].Comment, Is.EqualTo("comment inline to msgid"));
            Assert.That(lines[2].Keyword.Keyword, Is.EqualTo("msgid"));
            Assert.That(lines[3].Comment, Is.EqualTo("comment between keywords"));
            Assert.That(lines[5].Comment, Is.EqualTo("comment at end"));
        }

        [Test]
        public void PostKeywordCommentsPartOfFirstEntry_WhenNoSpaceBetweenFirstAndSecondEntry()
        {
            string document = genericHeader + @"
# comment at start
msgid ""foo"" #comment inline to msgid
#comment between keywords
msgstr ""bar""
#comment at end
#another comment at end
msgid ""bar""
msgstr ""baz""";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            AssertNonHeaderEntries(getTextDocument, 2);
            Assert.That(getTextDocument.Entries[1].Lines.Last().Comment, Is.EqualTo("another comment at end"));
            Assert.That(getTextDocument.Entries[2].Lines.First().Keyword.Keyword, Is.EqualTo("msgid"));
        }

        [Test]
        public void PostKeywordCommentsSplitByLine()
        {
            string document = genericHeader + @"
# comment at start
msgid ""foo"" #comment inline to msgid
#comment between keywords
msgstr ""bar""
#comment at end
#another comment at end

#comment at start
msgid ""bar""
msgstr ""baz""";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            AssertNonHeaderEntries(getTextDocument, 2);
            Assert.That(getTextDocument.Entries[1].Lines.Last().Comment, Is.EqualTo("another comment at end"));
            Assert.That(getTextDocument.Entries[2].Lines.First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(1).First().Comment, Is.EqualTo("comment at start"));
        }

        [Test]
        public void SecondEntryIncludesWhitespace_AfterFirstEntrySplitOffByWhitespace()
        {
            string document = genericHeader + @"
msgid ""foo""
msgstr ""bar""

#comment 1

#comment 2
#comment 3
msgid ""bar""


msgstr ""baz""";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            AssertNonHeaderEntries(getTextDocument, 2);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(0).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(1).First().Comment, Is.EqualTo("comment 1"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(2).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(3).First().Comment, Is.EqualTo("comment 2"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(4).First().Comment, Is.EqualTo("comment 3"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(6).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(7).First().IsWhiteSpace, Is.True);
        }

        [Test]
        public void LastEntryIncludesDoesNotSplitByWhitespace()
        {
            string document = genericHeader + @"
msgid ""foo""
msgstr ""bar""

#comment 1


#comment 2

";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            AssertNonHeaderEntries(getTextDocument, 1);
            Assert.That(getTextDocument.Entries[1].Lines.Skip(0).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[1].Lines.Skip(3).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[1].Lines.Skip(4).First().Comment, Is.EqualTo("comment 1"));
            Assert.That(getTextDocument.Entries[1].Lines.Skip(5).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[1].Lines.Skip(6).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[1].Lines.Skip(7).First().Comment, Is.EqualTo("comment 2"));
            Assert.That(getTextDocument.Entries[1].Lines.Skip(8).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[1].Lines.Count, Is.EqualTo(9));
            //due to textreader behavior, the extra line isn't returned
            //if we really cared, we could handle reading line some other way, but it's arguably not particularly important
        }

        [Test]
        public void ParsesMiddleObsoleteEntry()
        {
            string document = genericHeader + @"
msgid ""foo""
msgstr ""bar""

#comment
#~ msgid ""bar""
#comment
#~ msgstr ""baz""
#~ ""something"" #inline comment
#comment

msgid ""baz""
msgstr ""wat""";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            AssertNonHeaderEntries(getTextDocument, 3);
            Assert.That(getTextDocument.Entries[2].Id, Is.EqualTo("bar"));
            Assert.That(getTextDocument.Entries[2].IsObsolete, Is.True);
            Assert.That(getTextDocument.Entries[2].Value, Is.EqualTo("bazsomething"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(0).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(1).First().Comment, Is.EqualTo("comment"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(3).First().Comment, Is.EqualTo("comment"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(5).First().Comment, Is.EqualTo("~ \"something\" #inline comment"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(6).First().Comment, Is.EqualTo("comment"));
        }

        [Test]
        public void ParsesConsecutiveObsoleteEntries()
        {
            string document = genericHeader + @"
#comment
#~ msgid ""a""
#comment
#~ msgstr ""baz""
#~ ""something"" #inline comment
#comment

#comment
#~ msgid ""b""
#comment
#~ msgstr ""baz""
#~ ""something"" #inline comment
#comment

#comment
#~ msgid ""c""
#comment
#~ msgstr ""baz""
#~ ""something"" #inline comment
#comment";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            AssertNonHeaderEntries(getTextDocument, 3);
            Assert.That(getTextDocument.Entries[1].Id, Is.EqualTo("a"));
            Assert.That(getTextDocument.Entries[2].Id, Is.EqualTo("b"));
            Assert.That(getTextDocument.Entries[3].Id, Is.EqualTo("c"));
            for (int i = 1; i < 4; i++)
            {
                Assert.That(getTextDocument.Entries[i].IsObsolete, Is.True);
                Assert.That(getTextDocument.Entries[i].Value, Is.EqualTo("bazsomething"));
                Assert.That(getTextDocument.Entries[i].Lines.Skip(0).First().IsWhiteSpace, Is.True);
                Assert.That(getTextDocument.Entries[i].Lines.Skip(1).First().Comment, Is.EqualTo("comment"));
                Assert.That(getTextDocument.Entries[i].Lines.Skip(3).First().Comment, Is.EqualTo("comment"));
                Assert.That(getTextDocument.Entries[i].Lines.Skip(5).First().Comment, Is.EqualTo("~ \"something\" #inline comment"));
                Assert.That(getTextDocument.Entries[i].Lines.Skip(6).First().Comment, Is.EqualTo("comment"));
            }
        }

        [Test]
        public void ParsesObsoleteEntry_WithMidEntryWhitespace()
        {
            string document = genericHeader + @"
msgid ""foo""
msgstr ""bar""

#comment

#~ msgid ""bar""

#comment

#~ msgstr ""baz""

#~ ""something"" #inline comment
#comment

msgid ""baz""
msgstr ""wat""";

            GetTextDocument getTextDocument = GetTextDocument.Parse(document);

            AssertNonHeaderEntries(getTextDocument, 3);
            Assert.That(getTextDocument.Entries[2].Id, Is.EqualTo("bar"));
            Assert.That(getTextDocument.Entries[2].IsObsolete, Is.True);
            Assert.That(getTextDocument.Entries[2].Value, Is.EqualTo("bazsomething"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(0).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(1).First().Comment, Is.EqualTo("comment"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(2).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(4).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(5).First().Comment, Is.EqualTo("comment"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(6).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(8).First().IsWhiteSpace, Is.True);
            Assert.That(getTextDocument.Entries[2].Lines.Skip(9).First().Comment, Is.EqualTo("~ \"something\" #inline comment"));
            Assert.That(getTextDocument.Entries[2].Lines.Skip(10).First().Comment, Is.EqualTo("comment"));
        }

        private static GetTextEntry AssertHasEntry(GetTextDocument getTextDocument, string id)
            => AssertHasEntry(getTextDocument, context: null, id: id);
        private static GetTextEntry AssertHasEntry(GetTextDocument getTextDocument, string context, string id)
        {
            GetTextEntry[] entries = getTextDocument.Entries.Where(e => e.Context == context && e.Id == id).ToArray();
            Assert.That(entries, Has.Length.EqualTo(1));

            return entries[0];
        }

        private static void AssertNonHeaderEntries(GetTextDocument getTextDocument, int count)
            => Assert.That(getTextDocument.Entries, Has.Count.EqualTo(count + 1));
    }
}
