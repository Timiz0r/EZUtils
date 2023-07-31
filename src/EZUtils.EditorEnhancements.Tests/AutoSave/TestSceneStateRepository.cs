namespace EZUtils.EditorEnhancements.AutoSave.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine.SceneManagement;

    public class TestSceneStateRepository : ISceneRecoveryRepository
    {
        private IReadOnlyList<EditorSceneRecord> sceneRecords = Array.Empty<EditorSceneRecord>();
        private bool performUnityCrashSimulation = false;
        private bool blockSceneRecordUpdates = false;

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

        public int GetAvailableAutoSaveCount(Scene scene)
        {
            DirectoryInfo autoSaveFolder = new DirectoryInfo(SceneAutoSaver.GetAutoSavePath(scene.path));
            return !autoSaveFolder.Exists
                ? 0
                : autoSaveFolder.GetFiles("*.unity").Length;
        }

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
    }
}
