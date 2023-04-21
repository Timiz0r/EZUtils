namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text.RegularExpressions;

    public class GetTextEntryBuilder
    {
        private ImmutableList<GetTextLine> lines = ImmutableList<GetTextLine>.Empty;
        //gave extra long names because doing a lot of copy-pasting with similarly-named parameters
        private string context;
        private string id;
        private string pluralId;
        private string value;
        private readonly List<string> pluralValues = new List<string>();

        public GetTextEntryBuilder AddComment(string comment)
        {
            AddLine(new GetTextLine(comment: comment));
            return this;
        }

        public GetTextEntryBuilder ConfigureContext(string context, string inlineComment = null)
        {
            if (this.context != null) throw new InvalidOperationException("Can only set the context once.");

            //since context is optional, we'll allow these calls
            if (context == null) return this;

            this.context = context;
            AddKeywordedLine("msgctxt", context, inlineComment: inlineComment);
            return this;
        }

        public GetTextEntryBuilder ConfigureId(string id, string inlineComment = null)
        {
            if (this.id != null) throw new InvalidOperationException("Can only set the id once.");
            this.id = id ?? throw new ArgumentNullException(nameof(id), "Entry ids cannot be null.");

            AddKeywordedLine("msgid", id, inlineComment: inlineComment);
            return this;
        }

        public GetTextEntryBuilder ConfigureAsPlural(string pluralId, string inlineComment = null)
        {
            if (this.pluralId != null) throw new InvalidOperationException("Can only set as plural once.");
            this.pluralId = pluralId ?? throw new ArgumentNullException(nameof(pluralId), "Plural ids cannot be null.");

            AddKeywordedLine("msgid_plural", pluralId, inlineComment: inlineComment);
            return this;
        }

        public GetTextEntryBuilder ConfigureAsPlural(string pluralId, int pluralForms, string inlineComment = null)
        {
            if (this.pluralId != null) throw new InvalidOperationException("Can only set as plural once.");
            this.pluralId = pluralId ?? throw new ArgumentNullException(nameof(pluralId), "Plural ids cannot be null.");

            AddKeywordedLine("msgid_plural", pluralId, inlineComment: inlineComment);
            for (int i = 0; i < pluralForms; i++)
            {
                _ = ConfigureAdditionalPluralValue(string.Empty);
            }
            return this;
        }

        public GetTextEntryBuilder ConfigureAsPlural(string pluralId, Locale locale, string inlineComment = null)
        {
            if (this.pluralId != null) throw new InvalidOperationException("Can only set as plural once.");
            this.pluralId = pluralId ?? throw new ArgumentNullException(nameof(pluralId), "Plural ids cannot be null.");

            AddKeywordedLine("msgid_plural", pluralId, inlineComment: inlineComment);

            AddEmptyPluralForm("Zero", locale.PluralRules.Zero);
            AddEmptyPluralForm("One", locale.PluralRules.One);
            AddEmptyPluralForm("Two", locale.PluralRules.Two);
            AddEmptyPluralForm("Few", locale.PluralRules.Few);
            AddEmptyPluralForm("Many", locale.PluralRules.Many);
            AddEmptyPluralForm("Other", locale.PluralRules.Other);
            AddSpecialZeroPluralForm();

            return this;

            void AddEmptyPluralForm(string pluralRuleName, string pluralRule)
            {
                if (pluralRule == null) return;

                AddLine(new GetTextLine(comment: $" plural[{pluralValues.Count + 1}]: {pluralRuleName}; {pluralRule}"));
                _ = ConfigureAdditionalPluralValue(string.Empty);
            }

            void AddSpecialZeroPluralForm()
            {
                if (!locale.UseSpecialZero) return;
                AddLine(new GetTextLine(
                    comment: $" plural[{pluralValues.Count + 1}]: special zero; use in case of a special message for zero elements"));
                _ = ConfigureAdditionalPluralValue(string.Empty);
            }
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

        public GetTextEntryBuilder AddEmptyLine()
        {
            AddLine(GetTextLine.Empty);

            return this;
        }

        public GetTextEntry Create() => new GetTextEntry(
            lines: lines,
            header: GetTextEntryHeader.ParseEntryLines(lines),
            isObsolete: false,
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
                AddLine(new GetTextLine(
                    new GetTextKeyword(keyword, index),
                    GetTextString.FromValue(value),
                    comment: inlineComment));
                return;
            }

            //in multiline mode, $ eats up \n, so we dont use it
            //we want to maintain newline chars
            string[] values = Regex.Matches(value, @"(?m)^[^\r\n]+\r?\n?").Cast<Match>().Select(m => m.Value).ToArray();
            AddLine(new GetTextLine(
                new GetTextKeyword(keyword, index),
                GetTextString.FromValue(values[0]),
                comment: inlineComment));
            foreach (string additionalLine in values.Skip(1))
            {
                AddLine(new GetTextLine(GetTextString.FromValue(additionalLine)));
            }
        }

        private void AddLine(GetTextLine line) => _ = ImmutableInterlocked.Update(ref lines, (ls, l) => ls.Add(l), line);
    }
}
