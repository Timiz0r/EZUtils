namespace EZUtils.VPMUnityPackage
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine.UIElements;

    public class VPMPackageCreatorEditorWindow : EditorWindow
    {
        private const string RepositoryUrlPrefKey = "EZUtils.VPMUnityPackage.RepositoryUrl";
        private const string ScopedRepositoryScopePrefKey = "EZUtils.VPMUnityPackage.ScopedRepositoryScope";

        [MenuItem("EZUtils/VPM .unitypackage Creator", isValidateFunction: false, priority: 0)]
        public static void PackageManager()
        {
            VPMPackageCreatorEditorWindow window =
                GetWindow<VPMPackageCreatorEditorWindow>("EZUtils VPM .unitypackage Creator");
            window.Show();
        }
        [MenuItem("EZUtils/VPM .unitypackage Creator", isValidateFunction: true, priority: 0)]
        public static bool ValidatePackageManager() => File.Exists("Assets/EZUtils/BootstrapPackage/Editor/Development.txt");

        public async void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.vpmunitypackage/VPMPackageCreatorEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            string generationRoot = EnsureFolderCreated("Assets/EZUtils/BootstrapPackage/Editor/VPMUnityPackage");

            TextField repositoryUrlField = rootVisualElement.Q<TextField>(name: "repositoryUrl");
            repositoryUrlField.SetValueWithoutNotify(EditorPrefs.GetString(RepositoryUrlPrefKey));
            _ = repositoryUrlField.RegisterValueChangedCallback(
                evt => EditorPrefs.SetString(RepositoryUrlPrefKey, evt.newValue));

            TextField packageNameField = rootVisualElement.Q<TextField>(name: "packageName");
            TextField scopedRepositoryScopeField = rootVisualElement.Q<TextField>(name: "scopedRepositoryScope");
            scopedRepositoryScopeField.SetValueWithoutNotify(EditorPrefs.GetString(ScopedRepositoryScopePrefKey));
            _ = scopedRepositoryScopeField.RegisterValueChangedCallback(
                evt => EditorPrefs.SetString(ScopedRepositoryScopePrefKey, evt.newValue));

            Button createButton = rootVisualElement.Q<Button>();

            UIValidator uiValidator = new UIValidator();
            uiValidator.AddValueValidation(repositoryUrlField, v => !string.IsNullOrEmpty(v));
            uiValidator.AddValueValidation(packageNameField, v => !string.IsNullOrEmpty(v));
            uiValidator.DisableIfInvalid(createButton);

            createButton.clicked += () =>
            {
                string targetPackageName = packageNameField.value;
                string packageFolder = EnsureFolderCreated(Path.Combine(generationRoot, targetPackageName));

                _ = CreatePackage(
                    Path.Combine(packageFolder, $"VPM-{targetPackageName}.unitypackage"),
                    CopyScript("UPMPackageClient.cs"),
                    CopyScript("VPMPackageInstaller.cs"));

                string CreatePackage(string packagePath, params string[] paths)
                {
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        AssetDatabase.ExportPackage(
                            paths,
                            packagePath);
                        return packagePath;
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                        AssetDatabase.Refresh();
                    }
                }

                string CopyScript(string relativePath)
                {
                    string sourcePath = Path.Combine("Packages/com.timiz0r.ezutils.vpmunitypackage", relativePath);
                    string destinationPath = Path.Combine(packageFolder, Path.GetFileName(relativePath));
                    if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    {
                        throw new InvalidOperationException($"Failed to copy '{sourcePath}' to '{destinationPath}'.");
                    }
                    ReplaceContents(
                        destinationPath,
                        (@"namespace EZUtils\.VPMUnityPackage", $"namespace EZUtils.VPM.{targetPackageName.Replace(".", "_")}"),
                        (@"using EZUtils\.VPMUnityPackage;", $"using EZUtils.VPM.{targetPackageName.Replace(".", "_")}"),
                        ("{TargetRepoUrl}", repositoryUrlField.value),
                        ("{TargetPackageName}", targetPackageName),
                        ("{TargetScopedRegistryScope}", scopedRepositoryScopeField.value));
                    return destinationPath;
                }

                void ReplaceContents(string path, params (string pattern, string replacement)[] replacements)
                {
                    string currentContents = File.ReadAllText(path);
                    foreach ((string pattern, string replacement) in replacements)
                    {
                        currentContents = Regex.Replace(
                            currentContents,
                            pattern,
                            replacement);
                    }
                    File.WriteAllText(path, currentContents);
                }
            };
        }

        //similar to ezfxlayer, except wedont use assetdatabase to see if folder exists
        //when we're generating, we're turning off importing, and IsValidFolder doesnt seem to work in that case
        private static string EnsureFolderCreated(string path)
        {
            if (!Regex.IsMatch(path, @"^Assets[/\\]")) throw new ArgumentOutOfRangeException(
                nameof(path), $"Path '{path}' is not rooted in Assets.");
            if (Directory.Exists(path)) return path;

            string[] splitPath = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string currentPath = "Assets";
            foreach (string pathComponent in splitPath.Skip(1))
            {
                //since unity uses /, avoid Path.Combine in case windows adds \
                string targetPath = $"{currentPath}/{pathComponent}";
                if (!Directory.Exists(targetPath))
                {
                    _ = AssetDatabase.CreateFolder(currentPath, pathComponent);
                }
                currentPath = targetPath;
            }
            return currentPath;
        }
    }
}
