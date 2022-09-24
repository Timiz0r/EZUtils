namespace EZUtils.Bootstrap.com_timiz0r_ezutils_ezfxlayer
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using System;
    using System.IO;
    using UnityEngine;
    using System.Threading.Tasks;

    public static class BootstrapperPass2
    {
        private const string TargetPackageName = "com.timiz0r.ezutils.ezfxlayer";

        [InitializeOnLoadMethod]
        public static async void Run()
        {
            string finishedFilePath = $"Assets/EZUtils/BootstrapPackage/{TargetPackageName}/Finished.txt";
            if (File.Exists("Assets/EZUtils/BootstrapPackage/Block.txt")) return;
            if (File.Exists(finishedFilePath)) return;

            PackageRepository repo = new PackageRepository();
            repo.CheckForScopedRegistry();

            IReadOnlyList<UnityEditor.PackageManager.PackageInfo> installedPackages =
                await UPMPackageClient.ListAsync(offlineMode: true);
            IReadOnlyList<PackageInformation> packages = await repo.ListAsync(showPreRelease: false);

            await Install(TargetPackageName);
            await Install("com.timiz0r.ezutils.packagemanager");

            File.Create(finishedFilePath).Dispose();

            Task Install(string packageName)
            {
                if (installedPackages.Any(p => p.name == packageName))
                {
                    Debug.Log($"Package '{packageName}' is already installed.");
                    return Task.CompletedTask;
                }

                PackageInformation targetPackage = packages.SingleOrDefault(p => p.Name == TargetPackageName);
                if (targetPackage == null) throw new InvalidOperationException(
                    $"Could not install package '{TargetPackageName}'.");
                return repo.SetVersionAsync(targetPackage, targetPackage.Versions[0]);
            }
        }
    }
}
