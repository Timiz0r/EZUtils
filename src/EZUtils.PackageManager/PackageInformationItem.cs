namespace EZUtils.PackageManager.UIElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    internal class PackageInformationItem : VisualElement
    {
        private readonly List<PackageVersion> packageVersions = new List<PackageVersion>()
        {
            //the popup field requires a default item, for some reason
            new PackageVersion(0, 0, 1, "unknown")
        };
        private PackageInformation packageInformation;

        public PackageInformationItem(PackageRepository packageRepository, Func<Task> refresher)
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.packagemanager/PackageInformationItem.uxml");
            visualTree.CloneTree(this);

            PopupField<PackageVersion> versionPopup = new PopupField<PackageVersion>(
                packageVersions,
                packageVersions[0],
                v => v.FullVersion,
                v => v.FullVersion
            );
            _ = versionPopup.RegisterValueChangedCallback(evt =>
            {
                if (packageInformation.InstalledVersion == null) return;

                int comparison = packageInformation.InstalledVersion.CompareTo(evt.newValue);
                EnableInClassList("package-downgrade-selected", comparison > 0);
                EnableInClassList("package-upgrade-selected", comparison < 0);
            });
            this.Q<VisualElement>(className: "version-selector-container").Add(versionPopup);

            async Task PackageOperation()
            {
                await packageRepository.SetVersionAsync(packageInformation, versionPopup.value);
                await refresher();
            }
            this.Query<Button>(className: "package-modification-operation")
                .ForEach(b => b.clicked += async () => await PackageOperation());

            this.Q<Button>(name: "uninstallPackage").clicked += async () =>
            {
                await packageRepository.RemoveAsync(packageInformation);
                await refresher();
            };
        }

        public void Rebind(
            PackageInformation packageInformation)
        {
            this.packageInformation = packageInformation;

            packageVersions.Clear();
            packageVersions.AddRange(packageInformation.Versions);

            PopupField<PackageVersion> packageVersionPopup = this.Q<PopupField<PackageVersion>>();
            packageVersionPopup.value = packageInformation.InstalledVersion ?? packageVersions.First();
            packageVersionPopup.SetEnabled(packageInformation.IsAvailable);
            this.Query<Button>(className: "package-modification-operation")
                .ForEach(e => e.SetEnabled(packageInformation.IsAvailable));

            EnableInClassList("package-installed", packageInformation.InstalledVersion != null);
            EnableInClassList("package-uninstalled", packageInformation.InstalledVersion == null);

            this.Q<Label>(className: "package-name").text = packageInformation.Name;

            this.Q<Label>(className: "package-status").text =
                packageInformation.InstalledVersion == null
                    ? "Not installed"
                    : $"Version {packageInformation.InstalledVersion.FullVersion} installed";
        }
    }
}
