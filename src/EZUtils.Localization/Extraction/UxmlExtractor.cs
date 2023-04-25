namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;

    public class UxmlExtractor
    {
        private readonly GetTextCatalogBuilder catalogBuilder;

        public UxmlExtractor(GetTextCatalogBuilder catalogBuilder)
        {
            this.catalogBuilder = catalogBuilder;
        }

        public void ExtractAll(string root)
        {
            DirectoryInfo rootDir = new DirectoryInfo(root);
            if (!rootDir.Exists) throw new ArgumentOutOfRangeException(nameof(root));
            foreach (FileInfo file in rootDir.EnumerateFiles("*.uxml", SearchOption.AllDirectories))
            {
                using (StreamReader sr = new StreamReader(file.OpenRead()))
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
                        if (xml.NodeType == XmlNodeType.Element && xml.Name == "GenerateLanguage")
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

                                string referencePath = PathUtil.GetRelative(rootDir.FullName, file.FullName, newRoot: root);
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
                                    _ = catalogBuilder.ForEachDocument(doc =>
                                    {
                                        //if uxml dir does not start with po file dir
                                        //in the event a GenerateLanguage is not specified, we try to find docs
                                        //in the current or sub directories
                                        if (!Path.GetDirectoryName(referencePath).StartsWith(
                                            Path.GetDirectoryName(doc.Path),
                                            StringComparison.OrdinalIgnoreCase)) return;

                                        AddEntry(doc);
                                    });
                                }
                                void AddEntry(GetTextDocumentBuilder doc) => doc
                                    .AddEntry(e => _ = e
                                        .AddEmptyLine() //entries tend to have whitespace on top to visually separate them
                                        .AddHeaderReference(referencePath, lineInfo.LineNumber)
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
