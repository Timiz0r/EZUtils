namespace EZUtils
{
    using System;
    using System.Globalization;
    using UnityEditor;

    public class EditorPreference<T> : IEquatable<EditorPreference<T>>
        where T : IConvertible, IEquatable<T>
    {
        private readonly string key;
        private readonly T defaultValue;

        public EditorPreference(string key) : this(key, default)
        {
        }

        public EditorPreference(string key, T defaultValue)
        {
            this.key = key;
            this.defaultValue = defaultValue;
        }

        public T Value
        {
            get => RawValue == null
                ? defaultValue
                : (T)((IConvertible)RawValue).ToType(typeof(T), CultureInfo.InvariantCulture);
            //worth noting for T==string that EditorPrefs will store it internally as empty string
            //we could work around it with special sauce, but not worth it
            //
            //if needed, we could also introduce formatting and formatter fields
            //but since this class is meant to store data transparently, there's no particular need
            set => EditorPrefs.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        //GetString returns empty string if key doesnt exist
        //under the assumption that the key will usually exist, we only call HasKey if we get an empty string
        //where we return null to disambiguate from storing an empty string
        public string RawValue
            => EditorPrefs.GetString(key) is string rawValue && rawValue.Length > 0
                ? rawValue
                : EditorPrefs.HasKey(key) ? string.Empty : null;

        public void Delete() => EditorPrefs.DeleteKey(key);

        public bool Equals(EditorPreference<T> other) => other != null && other.Value.Equals(Value);
        public override bool Equals(object obj) => obj is EditorPreference<T> other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(EditorPreference<T> lhs, EditorPreference<T> rhs)
            => object.ReferenceEquals(lhs, null) ? object.ReferenceEquals(rhs, null) : lhs.Equals(rhs);
        public static bool operator !=(EditorPreference<T> lhs, EditorPreference<T> rhs) => !(lhs == rhs);

        public override string ToString() => RawValue;
    }
}
