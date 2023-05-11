namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;

    internal class UxmlExtractor
    {
        private readonly GetTextCatalogBuilder catalogBuilder;

        public UxmlExtractor(GetTextCatalogBuilder catalogBuilder)
        {
            this.catalogBuilder = catalogBuilder;
        }

        public void Extract(string uxmlFilePath, string root)
        {
            using (StreamReader sr = new StreamReader(File.OpenRead(uxmlFilePath)))
            using (XmlReader xml = XmlReader.Create(sr))
            {
                //is a safe cast, since creating from a textreader means we get line info
                IXmlLineInfo lineInfo = (IXmlLineInfo)xml;

                //while 1 is an unusually small initial capacity, typical usages have 1 or 0 of them
                //so zero previousLanguages is actually most common
                Stack<GenerateLanguageRecord> previousLanguages = new Stack<GenerateLanguageRecord>(1);
                GenerateLanguageRecord currentLanguage = null;
                while (xml.Read())
                {
                    //could theoretically check namespace
                    if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "GenerateLanguage")
                    {
                        currentLanguage = ReadGenerateLanguageElement(out GenerateLanguageRecord oldLanguage);
                        if (oldLanguage != null)
                        {
                            previousLanguages.Push(oldLanguage);
                        }

                        continue;
                    }

                    if (xml.NodeType == XmlNodeType.EndElement && xml.Depth < currentLanguage?.DepthFound)
                    {
                        currentLanguage = previousLanguages.Count > 0 ? previousLanguages.Pop() : null;
                    }

                    while (xml.MoveToNextAttribute())
                    {
                        string value = xml.Value;
                        if (value.StartsWith("loc:", StringComparison.Ordinal))
                        {
                            Match match = Regex.Match(value, @"(?x)^
                                loc:
                                (?'context'(?:[^:]|::)+)?
                                (?(context):) #if we found a context, only then match a second colon
                                (?'id'.+)$");
                            string context = match.Groups["context"].Success ? match.Groups["context"].Value : null;
                            string id = match.Groups["id"].Value;

                            if (currentLanguage != null)
                            {
                                foreach ((string poFilePath, Locale locale) in currentLanguage.Languages)
                                {
                                    string path = Path.Combine(root, poFilePath);
                                    _ = catalogBuilder.ForPoFile(
                                        path, locale, changeLocaleIfDifferent: true, doc => AddEntry(doc));
                                }
                            }
                            else
                            {
                                //we do a best-effort attempt at finding the associated po files for the uxml files
                                //first, if all of the po files share the same directory, then we'll use all of them
                                //otherwise, we'll only use documents that are in the same or sub directory of the po file

                                //it's a bit of a roundabout way, but it's what we expose at the moment
                                string poFileDirectory = null;
                                bool usesCommonDirectory = true;
                                _ = catalogBuilder.ForEachDocument(doc =>
                                {
                                    if (!usesCommonDirectory) return;
                                    if (poFileDirectory == null)
                                    {
                                        poFileDirectory = Path.GetDirectoryName(doc.Path);
                                        return;
                                    }
                                    usesCommonDirectory = poFileDirectory == Path.GetDirectoryName(doc.Path);
                                });

                                _ = catalogBuilder.ForEachDocument(doc =>
                                {
                                    bool useCurrentDoc = usesCommonDirectory
                                        || Path.GetDirectoryName(uxmlFilePath).StartsWith(
                                            Path.GetDirectoryName(doc.Path),
                                            StringComparison.OrdinalIgnoreCase);
                                    if (!useCurrentDoc) return;

                                    AddEntry(doc);
                                });
                            }
                            void AddEntry(GetTextDocumentBuilder doc) => doc
                                .AddEntry(e => _ = e
                                    .AddEmptyLine() //entries tend to have whitespace on top to visually separate them
                                    .AddHeaderReference(uxmlFilePath, lineInfo.LineNumber)
                                    .ConfigureContext(context)
                                    .ConfigureId(id)
                                    .ConfigureValue(string.Empty));
                        }
                    }
                }

                GenerateLanguageRecord ReadGenerateLanguageElement(out GenerateLanguageRecord oldLanguage)
                {
                    int currentDepth = xml.Depth;
                    CultureInfo cultureInfo = null;
                    string poFilePath = null;
                    string zero = null, one = null, two = null, few = null, many = null, other = null;
                    while (xml.MoveToNextAttribute())
                    {
                        switch (xml.Name)
                        {
                            case "poFilePath":
                                poFilePath = xml.Value;
                                break;
                            case "cultureInfoCode":
                                cultureInfo = CultureInfo.GetCultureInfo(xml.Value);
                                break;
                            case "zero":
                                zero = xml.Value;
                                break;
                            case "one":
                                one = xml.Value;
                                break;
                            case "two":
                                two = xml.Value;
                                break;
                            case "few":
                                few = xml.Value;
                                break;
                            case "many":
                                many = xml.Value;
                                break;
                            case "other":
                                other = xml.Value;
                                break;
                            default:
                                throw new InvalidOperationException(
                                    $"Attribute '{xml.Name}' is not valid for <GenerateLanguage />.");
                        }
                    }

                    if (cultureInfo == null) throw new InvalidOperationException(
                        $"No cultureInfo attribute found for <GenerateLanguage />.");
                    if (poFilePath == null) throw new InvalidOperationException(
                        $"No poFilePath attribute found for <GenerateLanguage />.");
                    if (poFilePath.Length == 0) throw new InvalidOperationException(
                        $"poFilePath attribute is empty for <GenerateLanguage />.");

                    Locale locale = new Locale(
                        cultureInfo,
                        new PluralRules(
                            zero: zero,
                            one: one,
                            two: two,
                            few: few,
                            many: many,
                            other: other));

                    if (currentLanguage == null || currentLanguage.DepthFound != currentDepth)
                    {
                        oldLanguage = currentLanguage;
                        GenerateLanguageRecord record = new GenerateLanguageRecord(
                            new[] { (poFilePath, locale) },
                            currentDepth);
                        return record;
                    }

                    //at the same depth, we want to add to the set of languages
                    oldLanguage = null;
                    return currentLanguage.Add(poFilePath, locale);
                }
            }
        }

        private class GenerateLanguageRecord
        {
            public IReadOnlyList<(string poFilePath, Locale locale)> Languages { get; }
            public int DepthFound { get; }

            public GenerateLanguageRecord(IReadOnlyList<(string poFilePath, Locale locale)> languages, int depthFound)
            {
                Languages = languages;
                DepthFound = depthFound;
            }

            public GenerateLanguageRecord Add(string poFilePath, Locale locale)
                => new GenerateLanguageRecord(Languages.Append((poFilePath, locale)).ToArray(), DepthFound);
        }
    }
}
