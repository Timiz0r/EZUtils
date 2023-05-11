namespace EZUtils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;

    public class AssemblyDefinition
    {
        private readonly string assemblyRootFullPath;

        public string Name { get; }
        public string Root { get; }
        public Assembly Assembly { get; }
        public IReadOnlyList<Assembly> References { get; }

        public AssemblyDefinition(string name, string root, Assembly assembly, IReadOnlyList<Assembly> references)
        {
            Name = name;
            Root = root;
            Assembly = assembly;
            References = references;
            assemblyRootFullPath = Path.GetFullPath(root);
        }

        public IEnumerable<FileInfo> GetFiles(string fileFilter)
        {
            DirectoryInfo root = new DirectoryInfo(Root);
            return root
                    .EnumerateFiles(fileFilter, SearchOption.TopDirectoryOnly)
                    .Concat(root.EnumerateDirectories().SelectMany(d => EnumerateSubDirectories(d)));

            IEnumerable<FileInfo> EnumerateSubDirectories(DirectoryInfo subDirectory)
                => subDirectory.EnumerateFiles("*.asmdef", SearchOption.TopDirectoryOnly).Any()
                        ? Enumerable.Empty<FileInfo>()
                        : subDirectory.EnumerateFiles(fileFilter, SearchOption.TopDirectoryOnly)
                            .Concat(subDirectory
                                .EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                                .SelectMany(d => EnumerateSubDirectories(d)));
        }

        public string GetUnityPath(string fileAbsolutePath)
            => PathUtil.GetRelative(assemblyRootFullPath, fileAbsolutePath, newRoot: Root);

        public static IEnumerable<AssemblyDefinition> GetAssemblyDefinitions()
        {
            Dictionary<string, Assembly> assemblies =
                AppDomain.CurrentDomain.GetAssemblies().ToDictionary(a => a.GetName().Name, a => a);
            IEnumerable<AssemblyDefinition> assemblyDefinitions = AssetDatabase
                .FindAssets("t:AssemblyDefinitionAsset")
                .Select(id =>
                {
                    string pathToFile = AssetDatabase.GUIDToAssetPath(id);
                    string root = Path.GetDirectoryName(pathToFile).Replace('\\', '/');
                    Deserializer.Deserialize(pathToFile, out string assemblyName, out IReadOnlyList<string> referenceAssemblyNames);
                    Assembly[] references = referenceAssemblyNames
                        .Select(n => assemblies.TryGetValue(n, out Assembly reference) ? reference : null)
                        .Where(ra => ra != null)
                        .ToArray();
                    Assembly assembly = assemblies.TryGetValue(assemblyName, out Assembly a) ? a : null;

                    return new AssemblyDefinition(name: assemblyName, root, assembly, references);
                });
            return assemblyDefinitions;
        }

        private class Deserializer
        {
            public string name;
            public string[] references;

            public static void Deserialize(string assemblyDefinitionPath, out string name, out IReadOnlyList<string> references)
            {
                Deserializer deserializer = new Deserializer();     
                EditorJsonUtility.FromJsonOverwrite(
                    File.ReadAllText(assemblyDefinitionPath),
                    deserializer);
                name = deserializer.name;
                references = deserializer.references ?? Array.Empty<string>();
            }
        }
    }
}
