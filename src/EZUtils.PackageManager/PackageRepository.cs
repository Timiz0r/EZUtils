namespace EZUtils.PackageManager
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using UPM = UnityEditor.PackageManager;

    internal class PackageRepository
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public async Task<IReadOnlyList<PackageInformation>> ListAsync(bool showPreRelease)
        {
            HttpResponseMessage request = await httpClient.GetAsync(
                new Uri("https://feeds.dev.azure.com/timiz0r/EZUtils/_apis/packaging/Feeds/EZUtils/packages?protocolType=npm&packageNameQuery=com.timiz0r.ezutils&includeAllVersions=true"));
            if (!request.IsSuccessStatusCode) throw new InvalidOperationException(
                $"Failed to list packages: {request.StatusCode} {request.ReasonPhrase}");

            JObject content = JObject.Parse(await request.Content.ReadAsStringAsync());

            List<PackageInformation> results = new List<PackageInformation>();
            foreach (JObject package in content["value"].Values<JObject>())
            {
                string name = (string)package["name"];
                PackageVersion[] versions = package["versions"]
                    .Where(ver => ver["views"].Any(view => ((string)view["type"]) == "release"))
                    .Select(v => PackageVersion.Parse((string)v["version"]))
                    .OrderByDescending(v => v)
                    .ToArray();
                PackageVersion currentVersion = await GetCurrentlyUsedVersionAsync(versions, name);

                //we specifically filter out prerelease here because, if a pre-release version is in use, we still
                //want it in the result.
                versions = versions.Where(v => showPreRelease || string.IsNullOrEmpty(v.PreRelease)).ToArray();

                PackageInformation result = new PackageInformation(name, currentVersion, versions);
                results.Add(result);
            }

            return results;
        }

        public Task SetVersionAsync(PackageInformation packageInformation, PackageVersion targetVersion)
            => UPMPackageClient.AddAsync($"{packageInformation.Name}@{targetVersion.FullVersion}");

        public Task RemoveAsync(PackageInformation packageInformation)
            => UPMPackageClient.RemoveAsync(packageInformation.Name);

        public void CheckForScopedRegistry()
        {
            JObject manifest = JObject.Parse(File.ReadAllText(@"Packages\manifest.json"));
            JArray scopedRegistries = (JArray)manifest["scopedRegistries"];
            if (scopedRegistries == null)
            {
                manifest.Add("scopedRegistries", scopedRegistries = new JArray());
            }

            JObject scopedRegistry = scopedRegistries
                .Cast<JObject>()
                .SingleOrDefault(r => ((string)r["name"]) == "EZUtils");
            if (scopedRegistry == null)
            {
                scopedRegistries.Add(scopedRegistry = new JObject());
            }

            scopedRegistry["name"] = "EZUtils";
            scopedRegistry["url"] = "https://pkgs.dev.azure.com/timiz0r/EZUtils/_packaging/EZUtils/npm/registry";
            scopedRegistry["scopes"] = new JArray() { "com.timiz0r.ezutils" };

            File.WriteAllText(@"Packages\manifest.json", manifest.ToString());
        }

        private static async Task<PackageVersion> GetCurrentlyUsedVersionAsync(
            IEnumerable<PackageVersion> versions, string packageName)
        {
            IReadOnlyList<UPM.PackageInfo> packages = await UPMPackageClient.ListAsync(offlineMode: true);
            UPM.PackageInfo targetPackage = packages.FirstOrDefault(p => p.name == packageName);

            PackageVersion result = targetPackage == null
                ? null
                : PackageVersion.TryParse(targetPackage.version, out PackageVersion parsedVersion)
                    //we're effectively saying that if we have an unknown version, we'll consider it not installed
                    //either via the verson being unparsable, or via the version not existing in the repo (like in this repo!)
                    //installing will provide a known version, as well!
                    ? versions.SingleOrDefault(v => v == parsedVersion)
                    : null;
            return result;
        }
    }
}
