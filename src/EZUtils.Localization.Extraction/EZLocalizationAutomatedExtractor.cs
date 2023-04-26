namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    public class EZLocalizationAutomatedExtractor : AssetPostprocessor
    {
        [InitializeOnLoadMethod]
        private static void UnityInitialize() => EditorApplication.delayCall += DomainReloaded;

        private static void DomainReloaded() => PerformExtractionFor(AssemblyDefinition.GetAssemblyDefinitions());

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            string[] allPaths = importedAssets
                .Concat(deletedAssets)
                .Concat(movedAssets)
                .Concat(movedFromAssetPaths)
                .Where(p => p.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (allPaths.Length == 0) return;

            IEnumerable<AssemblyDefinition> assemblyDefinitions = AssemblyDefinition.GetAssemblyDefinitions()
                .Where(ad => allPaths.Any(p => p.StartsWith(ad.root, StringComparison.Ordinal)));
            PerformExtractionFor(assemblyDefinitions);
        }

        private static void PerformExtractionFor(IEnumerable<AssemblyDefinition> assemblyDefinitions)
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            EZLocalizationExtractor extractor = new EZLocalizationExtractor();
            foreach (AssemblyDefinition def in assemblyDefinitions
                .Where(ad => ad.assembly?.GetCustomAttribute<LocalizedAssemblyAttribute>() != null))
            {
                extractor.ExtractFrom(def.root);
                LocalizationProxyGenerator.Generate(def.root);
            }
            extractor.Finish();
            //particularly for proxy generation
            AssetDatabase.Refresh();

            stopwatch.Stop();
            Debug.Log($"Performed EZLocalization extraction in {stopwatch.ElapsedMilliseconds}ms.");
        }

        private class AssemblyDefinition
        {
            public string name;
            public string pathToFile;
            public string root;
            public Assembly assembly;

            public static IEnumerable<AssemblyDefinition> GetAssemblyDefinitions()
            {
                Dictionary<string, Assembly> assemblies =
                    AppDomain.CurrentDomain.GetAssemblies().ToDictionary(a => a.GetName().Name, a => a);
                IEnumerable<AssemblyDefinition> assemblyDefinitions = AssetDatabase
                    .FindAssets("t:AssemblyDefinitionAsset")
                    .Select(id =>
                    {
                        AssemblyDefinition def = new AssemblyDefinition()
                        {
                            pathToFile = AssetDatabase.GUIDToAssetPath(id)
                        };
                        def.root = Path.GetDirectoryName(def.pathToFile).Replace('\\', '/');

                        //reads in name
                        EditorJsonUtility.FromJsonOverwrite(
                            File.ReadAllText(def.pathToFile),
                            def);

                        def.assembly = assemblies.TryGetValue(def.name, out Assembly assembly) ? assembly : null;

                        return def;
                    });
                return assemblyDefinitions;
            }
        }
    }
}
