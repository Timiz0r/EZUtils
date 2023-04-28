namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    internal class EZLocalizationAutomatedExtractor : AssetPostprocessor
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
                .Where(ad => allPaths.Any(p => p.StartsWith(ad.Root, StringComparison.Ordinal)));
            PerformExtractionFor(assemblyDefinitions);
        }

        private static void PerformExtractionFor(IEnumerable<AssemblyDefinition> assemblyDefinitions)
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            EZLocalizationExtractor extractor = new EZLocalizationExtractor();
            foreach (AssemblyDefinition def in assemblyDefinitions
                .Where(ad => ad.Assembly?.GetCustomAttribute<LocalizedAssemblyAttribute>() != null))
            {
                extractor.ExtractFrom(def);
                LocalizationProxyGenerator.Generate(def);
            }
            extractor.Finish();
            //particularly for proxy generation
            AssetDatabase.Refresh();

            stopwatch.Stop();
            Debug.Log($"Performed EZLocalization extraction in {stopwatch.ElapsedMilliseconds}ms.");
        }
    }
}
