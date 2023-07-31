namespace EZUtils.EditorEnhancements.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;

    public class TestSceneStateRepository : ISceneRecoveryRepository, IDisposable
    {
        private IReadOnlyList<EditorSceneRecord> sceneRecords = Array.Empty<EditorSceneRecord>();
        private readonly List<AutoSavedSceneRecord> autoSavedScenes = new List<AutoSavedSceneRecord>();
        private bool performUnityCrashSimulation = false;
        private bool blockSceneRecordUpdates = false;

        public TestSceneStateRepository()
        {
            EditorSceneManager.sceneSaved += SceneSaved;
        }

        public IReadOnlyList<EditorSceneRecord> RecoverScenes()
        {
            //midCrashScenes exists because,
            blockSceneRecordUpdates = false;
            return sceneRecords;
        }

        public void UpdateScenes(IEnumerable<EditorSceneRecord> sceneRecords)
        {
            //when we simulate closing, the test will create/load a new scene, to imitate how unity starts with a clean scene
            //this will get picked up by SceneAutoSaver and cause scene records here to get updated, which we don't want,
            //since the way unity works in practice is that there are no events for the initial loaded scene
            //so, up until the next call to RecoverScenes, we'll throw away any updates
            if (blockSceneRecordUpdates) return;

            this.sceneRecords = sceneRecords.ToArray();
        }

        public bool MayPerformRecovery()
        {
            Assert.That(performUnityCrashSimulation, Is.True, "Some scene unexpectedly needs recovery.");
            performUnityCrashSimulation = false;
            return true;
        }

        public int GetAutoSaveCount(Scene scene) => autoSavedScenes.Count(a => a.OriginalPath == scene.path);

        public void SimulateUnityClose()
        {
            UpdateScenes(Enumerable.Empty<EditorSceneRecord>());
            performUnityCrashSimulation = false;
            blockSceneRecordUpdates = true;
        }

        public void SimulateUnityCrash()
        {
            performUnityCrashSimulation = true;
            blockSceneRecordUpdates = true;
        }

        public void Dispose()
        {
            EditorSceneManager.sceneSaved -= SceneSaved;

            foreach (string folder in autoSavedScenes.Select(a => Path.GetDirectoryName(a.AutoSavePath)).Distinct())
            {
                _ = AssetDatabase.DeleteAsset(folder);
            }
        }

        private void SceneSaved(Scene scene)
        {
            //since, for these unit tests, we wont be doing any other scene saving, this logic works fine
            //no need to differentiate other code or user-based save-asing
            //when auto-saving, we only save when dirty, and, since it's a save-as, it remains dirty
            if (!scene.isDirty) return;

            string autoSavePath = SceneAutoSaver.GetAutoSavePath(scene.path);
            DirectoryInfo autoSaveFolder = new DirectoryInfo(autoSavePath);
            Assert.That(autoSaveFolder.Exists, Is.True, $"Auto save folder '{autoSavePath}' does not exist.");

            int previousAutoSaves = autoSavedScenes.Count(s => s.OriginalPath == scene.path);
            FileInfo[] autoSaves = autoSaveFolder.GetFiles("*.unity");
            FileInfo newestAutoSave = autoSaves.OrderByDescending(f => f.CreationTime).First();
            autoSavedScenes.Add(new AutoSavedSceneRecord(scene.path, $"{autoSavePath}/{newestAutoSave.Name}"));

            Assert.That(
                autoSaves,
                Has.Length.EqualTo(previousAutoSaves + 1),
                $"Scene '{scene.name}' has '{previousAutoSaves}' previous auto saves, and this hasn't gone up despite another auto save.");
        }
    }
}
