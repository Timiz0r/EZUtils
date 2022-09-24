namespace EZUtils.Bootstrap.com_timiz0r_ezutils_ezfxlayer
{
    using System.Collections.Generic;

    public class PackageInformation
    {
        public PackageInformation(string name, PackageVersion installedVersion, IReadOnlyList<PackageVersion> versions)
        {
            Name = name;
            InstalledVersion = installedVersion;
            Versions = versions;
        }

        public string Name { get; }
        public PackageVersion InstalledVersion { get; }
        public IReadOnlyList<PackageVersion> Versions { get; }
    }
}
