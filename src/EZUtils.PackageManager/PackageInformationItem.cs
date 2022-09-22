namespace EZUtils.PackageManager.UIElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

        public PackageInformationItem(PackageRepository packageRepository, Action refresher)
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
                if (packageInformation.SelectedVersion == null) return;

                int comparison = packageInformation.SelectedVersion.CompareTo(evt.newValue);
                EnableInClassList("package-upgrade-selected", comparison > 0);
                EnableInClassList("package-downgrade-selected", comparison < 0);
            });
            this.Q<VisualElement>(className: "version-selector-container").Add(versionPopup);

            async void packageOperation()
            {
                await packageRepository.SetVersionAsync(packageInformation, versionPopup.value);
                refresher();
            }
            this
                .Query<Button>(className: "package-modification-operation")
                .ForEach(b => b.clicked += () => packageOperation());

            this.Q<Button>(name: "uninstallPackage").clicked += async () =>
            {
                await packageRepository.RemoveAsync(packageInformation);
            };
        }

        public void Rebind(
            PackageInformation packageInformation)
        {
            this.packageInformation = packageInformation;

            packageVersions.Clear();
            packageVersions.AddRange(packageInformation.Versions);

            this.Q<PopupField<PackageVersion>>().value = packageInformation.SelectedVersion ?? packageVersions.First();

            EnableInClassList("package-installed", packageInformation.SelectedVersion != null);
            EnableInClassList("package-uninstalled", packageInformation.SelectedVersion == null);

            this.Q<Label>(className: "package-name").text = packageInformation.Name;

            this.Q<Label>(className: "package-status").text =
                packageInformation.SelectedVersion == null
                    ? "Not installed"
                    : $"Version {packageInformation.SelectedVersion.FullVersion} installed";
        }
    }
}
