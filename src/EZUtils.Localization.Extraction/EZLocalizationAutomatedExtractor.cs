namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    internal class EZLocalizationAutomatedExtractor : AssetPostprocessor
    {
        [InitializeOnLoadMethod]
#pragma warning disable IDE0051 //Private member is unused; unity message
        private static void UnityInitialize() => EditorApplication.delayCall += DomainReloaded;
#pragma warning restore IDE0051

        private static void DomainReloaded()
            => PerformExtractionFor(AssemblyDefinition.GetAssemblyDefinitions().ToArray());

#pragma warning disable IDE0051 //Private member is unused; unity message
        private static void OnPostprocessAllAssets(
#pragma warning restore IDE0051
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
            PerformExtractionFor(assemblyDefinitions.ToArray());
        }

        private static void PerformExtractionFor(IReadOnlyList<AssemblyDefinition> assemblyDefinitions)
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            EZLocalizationExtractor extractor = new EZLocalizationExtractor(
                new UnityAssemblyRootResolver(assemblyDefinitions));
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
