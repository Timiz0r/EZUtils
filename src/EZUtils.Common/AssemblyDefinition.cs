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
        public string Name { get; }
        public string Root { get; }
        public Assembly Assembly { get; }

        public AssemblyDefinition(string name, string root, Assembly assembly)
        {
            Name = name;
            Root = root;
            Assembly = assembly;
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
                    string assemblyName = NameDeserializer.GetName(pathToFile);
                    Assembly assembly = assemblies.TryGetValue(assemblyName, out Assembly a) ? a : null;

                    return new AssemblyDefinition(name: assemblyName, root, assembly);
                });
            return assemblyDefinitions;
        }

        private class NameDeserializer
        {
            public string name;

            public static string GetName(string assemblyDefinitionPath)
            {
                NameDeserializer nameDeserializer = new NameDeserializer();
                EditorJsonUtility.FromJsonOverwrite(
                    File.ReadAllText(assemblyDefinitionPath),
                    nameDeserializer);
                return nameDeserializer.name;
            }
        }
    }
}
