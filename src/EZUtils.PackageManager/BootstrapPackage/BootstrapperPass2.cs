namespace EZUtils.PackageManager
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
        private const string TargetPackageName = "";

        [InitializeOnLoadMethod]
        public static async void Run()
        {
            if (string.IsNullOrEmpty(TargetPackageName)) return;
            if (File.Exists("Assets/EZUtils/BootstrapPackage/Editor/Development.txt")) return;
            string finishedFilePath = $"Assets/EZUtils/BootstrapPackage/Editor/{TargetPackageName}/Finished.txt";
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

                PackageInformation targetPackage = packages.SingleOrDefault(p => p.Name == packageName);
                if (targetPackage == null) throw new InvalidOperationException(
                    $"Could not install package '{packageName}'.");
                return repo.SetVersionAsync(targetPackage, targetPackage.Versions[0]);
            }
        }
    }
}
