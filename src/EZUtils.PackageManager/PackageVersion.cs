namespace EZUtils.PackageManager
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    public class PackageVersion : IComparable<PackageVersion>
    {
        private readonly IReadOnlyList<PreReleasePart> preReleaseParts = Array.Empty<PreReleasePart>();
        private readonly int hashcode;

        public PackageVersion(int major, int minor, int patch, string preRelease)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            PreRelease = preRelease;

            if (!string.IsNullOrEmpty(preRelease))
            {
                preReleaseParts = PreReleasePart.Parse(preRelease);
            }

            FullVersion = $"{major}.{minor}.{patch}-{preRelease}";
            hashcode = FullVersion.GetHashCode();
        }

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string PreRelease { get; }

        public string FullVersion { get; }

        public static PackageVersion Parse(string version) => TryParse(version, out PackageVersion result)
            ? result
            : throw new InvalidOperationException($"Attempted to parse invalid version: {version}.");

        public static bool TryParse(string version, out PackageVersion packageVersion)
        {
            packageVersion = null;

            Match match = Regex.Match(version, @"^(\d+)\.(\d+)\.(\d+)(?:-(.+))$");
            if (!match.Success) return false;

            int major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            int minor = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            int patch = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

            packageVersion = new PackageVersion(major, minor, patch, match.Groups[4].Value);
            return true;
        }

        public int CompareTo(PackageVersion other)
        {
            if (ReferenceEquals(other, null)) return 1;
            if (ReferenceEquals(this, other)) return 0;

            return
                  Major.CompareTo(other.Major) is int ma && ma != 0 ? ma
                : Minor.CompareTo(other.Minor) is int mi && mi != 0 ? mi
                : Major.CompareTo(other.Patch) is int p && p != 0 ? p
                : ComparePreReleaseParts();

            int ComparePreReleaseParts()
            {
                int zippedComparison = preReleaseParts
                    .Zip(other.preReleaseParts, (p1, p2) => p1.CompareTo(p2))
                    .FirstOrDefault(i => i != 0); //or 0 if all matching

                return zippedComparison != 0
                    ? zippedComparison
                    : preReleaseParts.Count.CompareTo(other.preReleaseParts.Count);
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;
            if (ReferenceEquals(obj, null))
                return false;

            PackageVersion other = (PackageVersion)obj;
            return CompareTo(other) == 0;
        }

        public override int GetHashCode() => hashcode;

        public static bool operator ==(PackageVersion left, PackageVersion right)
            => ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.Equals(right);

        public static bool operator !=(PackageVersion left, PackageVersion right) => !(left == right);

        public static bool operator <(PackageVersion left, PackageVersion right)
            => left == null ? !ReferenceEquals(right, null) : left.CompareTo(right) < 0;

        public static bool operator <=(PackageVersion left, PackageVersion right)
            => ReferenceEquals(left, null) || left.CompareTo(right) <= 0;

        public static bool operator >(PackageVersion left, PackageVersion right)
            => !ReferenceEquals(left, null) && left.CompareTo(right) > 0;

        public static bool operator >=(PackageVersion left, PackageVersion right)
            => ReferenceEquals(left, null) ? ReferenceEquals(right, null) : left.CompareTo(right) >= 0;

        private class PreReleasePart : IComparable<PreReleasePart>
        {
            private readonly int numeric = -1;
            private readonly string alphanumeric = null;

            public PreReleasePart(int numeric)
            {
                this.numeric = numeric;
            }
            public PreReleasePart(string alphanumeric)
            {
                this.alphanumeric = alphanumeric;
            }

            public static IReadOnlyList<PreReleasePart> Parse(string preRelease)
            {
                string[] parts = preRelease.Split('.');
                PreReleasePart[] results = parts
                    .Select(p => int.TryParse(p, out int n) ? new PreReleasePart(n) : new PreReleasePart(p))
                    .ToArray();
                return results;
            }

            public int CompareTo(PreReleasePart other)
            {
                if (other == null) return 1;
                if (this == other) return 0;

                bool isNumeric = alphanumeric == null;
                bool otherIsNumeric = other.alphanumeric == null;

                if (isNumeric && otherIsNumeric) return numeric.CompareTo(other.numeric);
                if (!isNumeric && !otherIsNumeric) return string.Compare(
                    alphanumeric, other.alphanumeric, StringComparison.Ordinal);

                if (!isNumeric && otherIsNumeric) return 1;
                if (isNumeric && !otherIsNumeric) return -1;

                throw new InvalidOperationException("Somehow can't compare PreReleaseParts.");;
            }
        }
    }
}
