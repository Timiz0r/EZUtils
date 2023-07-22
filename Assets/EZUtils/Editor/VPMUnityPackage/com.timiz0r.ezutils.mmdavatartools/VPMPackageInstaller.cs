namespace EZUtils.VPM.com_timiz0r_ezutils_mmdavatartools
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
        private static readonly bool IsTemplate = false;
        private static readonly string TargetRepoUrl = "https://timiz0r.github.io/EZUtils/";
        private static readonly string TargetPackageName = "com.timiz0r.ezutils.mmdavatartools";
        private static readonly string TargetScopedRegistryScope = "com.timiz0r.ezutils";

        public static string ScriptRoot(string packageName)
            => $"Assets/EZUtils/Editor/VPMUnityPackage/{packageName}";
        public static string ExecutionBlockingFile(string packageName)
            => $"{ScriptRoot(packageName)}/BlockExecution.txt";

        [InitializeOnLoadMethod]
        private static async void Run()
        {
            if (IsTemplate) return;
            if (File.Exists(ExecutionBlockingFile(TargetPackageName))) return;

            string packagesToReaddKey = $"{typeof(VPMPackageInstaller).FullName}.PackagesToReadd";
            IReadOnlyList<string> packagesToReadd = await RemoveDeprecatedScopedRegistry();
            if (packagesToReadd.Count > 0)
            {
                EditorPrefs.SetString(packagesToReaddKey, string.Join("\xff", packagesToReadd));

                //resolve doesnt work without the delay call
                EditorApplication.delayCall += () => UPMPackageClient.Resolve();

                //NOTE:
                //VPM won't place the packages in the packages folder if the upm packages still exist (with the same id)
                //therefore, they must be removed first. removing a package, one or many, results in a domain reload.
                //with the domain reload, we either need to write to the vpm manifest first, or we need to store
                //the package ids to be read back later, as we have chosen to do here.
                //we chose to keep state because we don't want to resolve all untargeted packages on behalf of the user,
                //just the ones in our scope.
                //
                //by returning here after we've modified manifest.json, and with the resolve call,
                //we'll get a domain reload, and this method will get called again
                return;
            }

            if (EditorPrefs.GetString(packagesToReaddKey) is string storedPackagesToReadd
                && string.IsNullOrEmpty(storedPackagesToReadd))
            {
                packagesToReadd = storedPackagesToReadd.Split('\xff');
                EditorPrefs.DeleteKey(packagesToReaddKey);
            }

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

            EditorApplication.delayCall += () =>
            {
                //resolve doesnt work without the delay call
                UPMPackageClient.Resolve();

                //a previous attempt put this before the delay call
                //the end result was the delay call getting eaten by a domain reload
                _ = AssetDatabase.DeleteAsset(ScriptRoot(TargetPackageName));
            };
        }

        //NOTE: it's mostly not feasible to avoid a newtonsoft.json assembly,
        //as the internal unity class for removing scoped registries changes between versions and would be a pain to use
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

                    //versus many remove operations (and apparently subsequent domain reloads)
                    //we'll just remove from the manifest and force a resolve later
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
