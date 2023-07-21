namespace EZUtils.VPMUnityPackage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using VRC.PackageManagement.Core;
    using VRC.PackageManagement.Core.Types;
    using VRC.PackageManagement.Core.Types.Packages;
    using VRC.PackageManagement.Core.Types.Providers;
    using UPM = UnityEditor.PackageManager;

    public static class VPMPackageInstaller
    {
        private static readonly string TargetRepoUrl = "{TargetRepoUrl}";
        private static readonly string TargetPackageName = "{TargetPackageName}";
        private static readonly string TargetScopedRegistryScope = "{TargetScopedRegistryScope}";

        [InitializeOnLoadMethod]
        public static async void Run()
        {
            if (File.Exists("Assets/EZUtils/BootstrapPackage/Editor/Development.txt")) return;

            IReadOnlyList<string> packagesToReadd = await RemoveDeprecatedScopedRegistry();
            Uri repoUrl = new Uri(TargetRepoUrl);

            if (!Repos.UserRepoExists(repoUrl))
            {
                _ = Repos.AddRepo(repoUrl, new Dictionary<string, string>());
            }

            VPMPackageProvider.SerializableCreationInfo targetRepoInfo =
                Repos.GetAllRepoInfo().SingleOrDefault(r => r.url == TargetRepoUrl);
            VPMPackageProvider targetRepo = VPMPackageProvider.Create(targetRepoInfo);
            IEnumerable<IVRCPackageProvider> packageProviders = Enumerable.Repeat(targetRepo, 1);
            IVRCPackage targetPackage = targetRepo.GetPackage(TargetPackageName);

            UnityProject vpmProject = new UnityProject(Directory.GetCurrentDirectory());
            foreach (string packageIdToReadd in packagesToReadd)
            {
                if (packageIdToReadd.Equals(targetPackage.Id, StringComparison.OrdinalIgnoreCase)) continue;

                IVRCPackage packageToReadd = targetRepo.GetPackage(packageIdToReadd);
                if (packageToReadd == null) continue;

                // System.Diagnostics.Debugger.Break();
                _ = vpmProject.AddVPMPackage(packageToReadd, packageProviders);
            }
            _ = vpmProject.AddVPMPackage(targetPackage, packageProviders);

            _ = AssetDatabase.DeleteAsset($"Assets/EZUtils/BootstrapPackage/Editor/VPMUnityPackage/{TargetPackageName}");
            EditorApplication.delayCall += () => UPMPackageClient.Resolve();
        }

        //NOTE: it's mostly not feasible to avoid a newtonsoft.json assembly
        //the internal unity class changes between versions and would be a pain to use
        //EditorJsonUtility is obnoxious to use when writing data without risking losing it or altering the look
        //
        //would also prefer an out instead of return type, but cant do that with async-await yet
        private static async Task<IReadOnlyList<string>> RemoveDeprecatedScopedRegistry()
        {
            List<string> packagesToReadd = new List<string>();
            if (string.IsNullOrEmpty(TargetScopedRegistryScope)) return packagesToReadd;

            JObject manifest = JObject.Parse(File.ReadAllText("Packages/manifest.json"));
            JArray scopedRegistries = (JArray)manifest["scopedRegistries"];
            JObject dependencies = (JObject)manifest["dependencies"];
            if (scopedRegistries == null)
            {
                return packagesToReadd;
            }

            JObject[] targetScopedRegistries = scopedRegistries
                .Cast<JObject>()
                .Where(sr => sr["scopes"].Any(scope => scope.ToString() == TargetScopedRegistryScope))
                .ToArray();
            if (targetScopedRegistries.Length == 0) return packagesToReadd;

            HashSet<string> targetScopedRegistryNames = new HashSet<string>(
                targetScopedRegistries.Select(sr => (string)sr["name"]));
            IReadOnlyList<UPM.PackageInfo> upmPackages =
                await UPMPackageClient.ListAsync(offlineMode: true);
            foreach (UPM.PackageInfo package in upmPackages)
            {
                if (targetScopedRegistryNames.Contains(package?.registry?.name))
                {
                    packagesToReadd.Add(package.name);

                    //we can't simply remove from the manifest because
                    //first, we need to delay-call a UPM resolve to get the packages to actually disappear.
                    //only then can will VPM actually install the packages.
                    //if the UPM packages are still around, they'll be in the vpm manifest but not placed in the dir.
                    //furthermore, since this results in a domain reload, it's not a simple as waiting.
                    //while there are workarounds, the slower but more sensical method is to remove from UPM.
                    await UPMPackageClient.RemoveAsync(package.name);

                    //reflect the upm remove operation back into our in-memory manifest
                    _ = dependencies.Remove(package.name);
                }
            }

            foreach (JObject targetScopedRegistry in targetScopedRegistries)
            {
                _ = scopedRegistries.Remove(targetScopedRegistry);
            }

            File.WriteAllText("Packages/manifest.json", manifest.ToString());

            return packagesToReadd;
        }
    }
}
