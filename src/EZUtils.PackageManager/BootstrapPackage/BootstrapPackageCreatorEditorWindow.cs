namespace EZUtils.PackageManager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using EZUtils.PackageManager.UIElements;
    using UnityEditor;
    using UnityEngine.UIElements;

    public class BootstrapPackageCreatorEditorWindow : EditorWindow
    {
        [MenuItem("EZUtils/Bootstrap Package Creator", isValidateFunction: false, priority: 0)]
        public static void PackageManager()
        {
            BootstrapPackageCreatorEditorWindow window =
                GetWindow<BootstrapPackageCreatorEditorWindow>("EZUtils Bootstrap Package Creator");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.packagemanager/BootstrapPackage/BootstrapPackageCreatorEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            File.Create(Path.Combine(EnsureFolderCreated("Assets/EZUtils/BootstrapPackage"), "Block.txt")).Dispose();

            rootVisualElement.Q<Button>().clicked += () =>
            {
                string targetPackageName = rootVisualElement.Q<TextField>().value ?? throw new InvalidOperationException(
                    "Cannot create a bootstrap package without specifying a package name.");

                string bootstrapPackageFolder = Path.Combine("Assets/EZUtils/BootstrapPackage", targetPackageName);

                AssetDatabase.StartAssetEditing();
                try
                {
                    string pass2Package =
                        Path.Combine(bootstrapPackageFolder, $"Pass2.unitypackage");
                    AssetDatabase.ExportPackage(
                        new[]
                        {
                            CopyScript("PackageVersion.cs", "Pass2"),
                            CopyScript("PackageRepository.cs", "Pass2"),
                            CopyScript("PackageInformation.cs", "Pass2"),
                            CopyBootstrapScript("BootstrapPackage/BootstrapperPass2.cs", "Pass2")
                        },
                        pass2Package);

                    string pass1Package =
                        Path.Combine(bootstrapPackageFolder, $"Install{targetPackageName}.unitypackage");
                    AssetDatabase.ExportPackage(
                        new[]
                        {
                            CopyScript("UPMPackageClient.cs", "Pass1"),
                            CopyBootstrapScript("BootstrapPackage/BootstrapperPass1.cs", "Pass1"),
                            pass2Package
                        },
                        pass1Package);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.Refresh();
                }

                string CopyScript(string relativePath, string pass)
                {
                    string passFolder = EnsureFolderCreated(Path.Combine(bootstrapPackageFolder, pass));
                    string destinationPath = Path.Combine(bootstrapPackageFolder, pass, Path.GetFileName(relativePath));
                    _ = AssetDatabase.CopyAsset(
                        Path.Combine("Packages/com.timiz0r.ezutils.packagemanager", relativePath),
                        destinationPath);
                    ReplaceContents(
                        destinationPath,
                        ("namespace EZUtils.PackageManager", $"namespace EZUtils.Bootstrap.{targetPackageName.Replace(".", "_")}"),
                        ("using EZUtils.PackageManager;", $"using EZUtils.Bootstrap.{targetPackageName.Replace(".", "_")}"));
                    return destinationPath;
                }

                string CopyBootstrapScript(string relativePath, string pass)
                {
                    string bootstrapScriptPath = CopyScript(relativePath, pass);
                    ReplaceContents(
                        bootstrapScriptPath,
                        ("(?m)const string TargetPackageName.+", $@"const string TargetPackageName = ""{targetPackageName}"";"));
                    return bootstrapScriptPath;
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
