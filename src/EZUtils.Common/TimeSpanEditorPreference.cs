namespace EZUtils
{
    using System;
    using System.Globalization;

    public class TimeSpanEditorPreference
    {
        private readonly EditorPreference<string> editorPreference;
        private readonly TimeSpan defaultValue;

        public TimeSpanEditorPreference(string key) : this(key, default)
        { }

        public TimeSpanEditorPreference(string key, TimeSpan defaultValue)
        {
            editorPreference = new EditorPreference<string>(key, defaultValue: null);
            this.defaultValue = defaultValue;
        }

        public TimeSpan Value
        {
            get => editorPreference.RawValue is string rawValue
                ? TimeSpan.ParseExact(rawValue, "c", CultureInfo.InvariantCulture)
                : defaultValue;

            set => editorPreference.Value = GetStringValue(value);
        }

        public string RawValue => editorPreference.RawValue;

        public void Delete() => editorPreference.Delete();

        public bool Equals(TimeSpanEditorPreference other) => other != null && other.Value.Equals(Value);
        public override bool Equals(object obj) => obj is TimeSpanEditorPreference other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(TimeSpanEditorPreference lhs, TimeSpanEditorPreference rhs)
            => object.ReferenceEquals(lhs, null) ? object.ReferenceEquals(rhs, null) : lhs.Equals(rhs);
        public static bool operator !=(TimeSpanEditorPreference lhs, TimeSpanEditorPreference rhs) => !(lhs == rhs);

        public override string ToString() => RawValue;
        public string ToString(string format, IFormatProvider formatProvider) => Value.ToString(format, formatProvider);

        private static string GetStringValue(TimeSpan value) => value.ToString("c", CultureInfo.InvariantCulture);
    }
}
