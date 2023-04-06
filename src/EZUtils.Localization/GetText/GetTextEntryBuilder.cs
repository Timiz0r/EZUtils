namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class GetTextEntryBuilder
    {
        private readonly List<GetTextLine> lines = new List<GetTextLine>();
        //gave extra long names because doing a lot of copy-pasting with similarly-named parameters
        private string context;
        private string id;
        private string pluralId;
        private string value;
        private readonly List<string> pluralValues = new List<string>();

        public GetTextEntryBuilder AddComment(string comment)
        {
            lines.Add(new GetTextLine(comment: comment));
            return this;
        }

        public GetTextEntryBuilder ConfigureContext(string context, string inlineComment = null)
        {
            if (this.context != null) throw new InvalidOperationException("Can only set the context once.");

            this.context = context;
            AddKeywordedLine("msgctxt", context, inlineComment: inlineComment);
            return this;
        }

        public GetTextEntryBuilder ConfigureId(string id, string inlineComment = null)
        {
            if (this.id != null) throw new InvalidOperationException("Can only set the id once.");

            this.id = id;
            AddKeywordedLine("msgid", id, inlineComment: inlineComment);
            return this;
        }

        public GetTextEntryBuilder ConfigureAsPlural(string pluralId, string inlineComment = null)
        {
            if (this.pluralId != null) throw new InvalidOperationException("Can only set as plural once.");

            this.pluralId = pluralId;
            AddKeywordedLine("msgid_plural", pluralId, inlineComment: inlineComment);
            return this;
        }

        public GetTextEntryBuilder ConfigureValue(string value, string inlineComment = null)
        {
            if (this.value != null) throw new InvalidOperationException("Can only set the value once.");

            this.value = value;
            AddKeywordedLine("msgstr", value, inlineComment: inlineComment);
            return this;
        }

        public GetTextEntryBuilder ConfigureAdditionalPluralValue(string value, string inlineComment = null)
        {
            AddKeywordedLine("msgstr", value, index: pluralValues.Count, inlineComment: inlineComment);
            pluralValues.Add(value);
            return this;
        }

        public GetTextEntry Create() => new GetTextEntry(
            lines: lines,
            header: null,
            context: context,
            id: id,
            pluralId: pluralId,
            value: value,
            pluralValues: pluralValues
        );

        //or multiple lines if newlines are present
        //conventionally, the file contains only line feeds, but read and split by whatever newline
        private void AddKeywordedLine(
            string keyword, string value, int? index = null, string inlineComment = null)
        {
            if (!value.Contains('\r') && !value.Contains('\n'))
            {
                lines.Add(
                    new GetTextLine(
                        new GetTextKeyword(keyword, index),
                        GetTextString.FromValue(value),
                        comment: inlineComment));
                return;
            }

            //dont worry about comments inline to string-only lines. could support it if desired though.
            string[] values = value.Split(new[] { "\r", "\n", "\r\n" }, StringSplitOptions.None);
            lines.Add(
                new GetTextLine(
                    new GetTextKeyword(keyword, index),
                    GetTextString.FromValue(values[0]),
                    comment: inlineComment));
            foreach (string additionalLine in values.Skip(1))
            {
                lines.Add(
                    new GetTextLine(
                        GetTextString.FromValue(additionalLine + "\n")));
            }
        }
    }
}
