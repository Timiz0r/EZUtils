namespace EZUtils.PackageManager
{
    using System.Collections.Generic;

    public class PackageInformation
    {
        public PackageInformation(string name, PackageVersion selectedVersion, IReadOnlyList<PackageVersion> versions)
        {
            Name = name;
            SelectedVersion = selectedVersion;
            Versions = versions;
        }

        public string Name { get; }
        public PackageVersion SelectedVersion { get; }
        public IReadOnlyList<PackageVersion> Versions { get; }
    }
}
