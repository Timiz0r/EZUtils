namespace EZUtils.EditorEnhancements.AutoSave
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    internal class SceneAssetPostProcessor : AssetPostprocessor
    {
        public delegate void SceneAssetMovedDelegate(string fromPath, string toPath);
        public static event SceneAssetMovedDelegate SceneAssetMoved;

#pragma warning disable IDE0051 //Private member is unused; unity message
        private static void OnPostprocessAllAssets(
#pragma warning restore IDE0051
#pragma warning disable IDE0060 //unused parameters
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
#pragma warning restore IDE0060
        {
            IEnumerable<(string fromPath, string toPath)> movedScenes = movedFromAssetPaths
                .Zip(movedAssets, (first, second) => (first, second))
                .Where(t => t.first.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
            foreach ((string fromPath, string toPath) in movedScenes)
            {
                DirectoryInfo fromAutoSaveFolder = new DirectoryInfo(SceneAutoSaver.GetAutoSavePath(fromPath));
                if (!fromAutoSaveFolder.Exists) continue;

                DirectoryInfo toAutoSaveFolder = new DirectoryInfo(SceneAutoSaver.GetAutoSavePath(toPath));
                toAutoSaveFolder.Create();
                foreach (FileInfo autoSave in fromAutoSaveFolder.EnumerateFiles("*.unity").Concat(fromAutoSaveFolder.EnumerateFiles("*.unity.meta")))
                {
                    autoSave.MoveTo(Path.Combine(toAutoSaveFolder.FullName, autoSave.Name));
                }
                fromAutoSaveFolder.Delete();

                foreach (SceneAssetMovedDelegate subscriber in SceneAssetMoved.GetInvocationList().Cast<SceneAssetMovedDelegate>())
                {
                    try
                    {
                        subscriber(fromPath, toPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }
    }
}
