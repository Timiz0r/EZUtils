namespace EZUtils.Localization
{
    using System;
    using System.IO;
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
            if (!rootDir.Exists) throw new ArgumentOutOfRangeException(nameof(rootDir));
            foreach (FileInfo file in rootDir.EnumerateFiles("*.uxml", SearchOption.AllDirectories))
            {
                using (StreamReader sr = new StreamReader(file.OpenRead()))
                using (XmlReader xml = XmlReader.Create(sr))
                {
                    //is a safe cast, since creating from a textreader means we get line info
                    IXmlLineInfo lineInfo = (IXmlLineInfo)xml;

                    while (xml.Read())
                    {
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

                                _ = catalogBuilder
                                    //we 
                                    .ForEachDocument(doc => doc
                                        .AddEntry(e => _ = e
                                            .AddEmptyLine() //entries tend to have whitespace on top to visually separate them
                                                            //TODO: add a header instead
                                            .AddComment($": {file.Name}:{lineInfo.LineNumber}")
                                            .ConfigureContext(context)
                                            .ConfigureId(id)));
                            }
                        }
                    }
                }
            }
        }
    }
}
