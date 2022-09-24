namespace EZUtils.PackageManager
{
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using UnityEditor;
    using UPM = UnityEditor.PackageManager;

    public static class BootstrapperPass1
    {
        private const string TargetPackageName = "ToBeReplaced";

        [InitializeOnLoadMethod]
        public static async void Run()
        {
            if (File.Exists("Assets/EZUtils/BootstrapPackage/Block.txt")) return;
            if (File.Exists($"Assets/EZUtils/BootstrapPackage/{TargetPackageName}/BootstrapperPass2.cs")) return;

            IReadOnlyList<UPM.PackageInfo> existingPackages = await UPMPackageClient.ListAsync(offlineMode: true);
            if (!existingPackages.Any(p => p.name == "com.unity.nuget.newtonsoft-json"))
            {
                //packagemanager depends on json lib
                //tho so does vrcsdk, so prob wont hit this in practice
                //we also prefer 2 because, at the time of writing, vrcsdk depends on it and not 3.
                _ = await UPMPackageClient.AddAsync("com.unity.nuget.newtonsoft-json@2.0.2");
            }

            AssetDatabase.ImportPackage(
                $"Assets/EZUtils/BootstrapPackage/{TargetPackageName}/Pass2.unitypackage", interactive: false);
        }
    }
}
