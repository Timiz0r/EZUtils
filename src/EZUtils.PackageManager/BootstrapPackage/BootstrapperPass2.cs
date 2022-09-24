namespace EZUtils.PackageManager
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using System;
    using System.IO;

    public static class BootstrapperPass2
    {
        private const string TargetPackageName = "ToBeReplaced";

        [InitializeOnLoadMethod]
        public static async void Run()
        {
            if (File.Exists("Assets/EZUtils/BootstrapPackage/Block.txt")) return;

            PackageRepository repo = new PackageRepository();
            repo.CheckForScopedRegistry();

            IReadOnlyList<PackageInformation> packages = await repo.ListAsync();

            PackageInformation targetPackage = packages.SingleOrDefault(p => p.Name == TargetPackageName);
            if (targetPackage == null) throw new InvalidOperationException(
                $"Could not install package '{TargetPackageName}'.");

            await repo.SetVersionAsync(targetPackage, targetPackage.Versions[0]);

            _ = AssetDatabase.DeleteAsset($"Assets/EZUtils/BootstrapPackage/{TargetPackageName}");
        }
    }
}
