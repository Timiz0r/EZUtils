namespace EZUtils.EditorEnhancements.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Object = UnityEngine.Object;

    public class SceneAutoSaveTests
    {
        [OneTimeSetUp]
        public static void OneTimeSetup() => AutoSave.Disable();

        [OneTimeTearDown]
        public void OneTimeTeardown() => AutoSave.Enable();

        [Test]
        public void AutoSave_DoesNotSave_WhenSceneNotDirty()
        {
            using (TestSceneStateRepository sceneRepository = new TestSceneStateRepository())
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            using (TestScene testScene = new TestScene("testscene"))
            {
                sceneAutoSaver.Load();

                sceneAutoSaver.AutoSave();

                Assert.That(sceneRepository.GetAutoSaveCount(testScene.Scene), Is.EqualTo(0));
            }
        }

        [Test]
        public void AutoSave_Saves_WhenSceneDirty()
        {
            using (TestSceneStateRepository sceneRepository = new TestSceneStateRepository())
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            using (TestScene testScene = new TestScene("testscene"))
            {
                sceneAutoSaver.Load();

                _ = new GameObject("test");
                testScene.MarkDirty();
                sceneAutoSaver.AutoSave();

                Assert.That(sceneRepository.GetAutoSaveCount(testScene.Scene), Is.EqualTo(1));
            }
        }
    }

    public class TestScene : IDisposable
    {
        private readonly string scenePath;
        private bool delete = false;

        public TestScene(string sceneName) : this(sceneName, deleteOnDispose: true)
        {
        }

        public TestScene(string sceneName, bool deleteOnDispose)
        {
            scenePath = $"Assets/{sceneName}.unity";
            delete = deleteOnDispose;

            if (File.Exists(scenePath))
            {
                Scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            else
            {
                Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _ = EditorSceneManager.SaveScene(Scene, scenePath);
            }
        }

        public Scene Scene { get; }

        public void MarkForDeletion() => delete = true;
        public void MarkDirty() => EditorSceneManager.MarkSceneDirty(Scene);

        public void Dispose()
        {
            //_ = EditorSceneManager.CloseScene(Scene, removeScene: true);
            //since we opened it single, we need another scene in order to close
            _ = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            if (delete) _ = AssetDatabase.DeleteAsset(scenePath);
        }
    }

    public class TestSceneStateRepository : ISceneStateRepository, IDisposable
    {
        private readonly List<AutoSavedSceneRecord> autoSavedScenes = new List<AutoSavedSceneRecord>();

        public TestSceneStateRepository()
        {
            EditorSceneManager.sceneSaved += SceneSaved;
        }

        public IReadOnlyList<EditorSceneRecord> Scenes { get; private set; } = new List<EditorSceneRecord>();

        public IReadOnlyList<EditorSceneRecord> RecoverScenes() => Scenes;

        public void UpdateScenes(IEnumerable<EditorSceneRecord> sceneRecords) => Scenes = sceneRecords.ToArray();

        public int GetAutoSaveCount(Scene scene) => autoSavedScenes.Count(a => a.OriginalPath == scene.path);

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

        public void Dispose()
        {
            EditorSceneManager.sceneSaved -= SceneSaved;

            foreach (string folder in autoSavedScenes.Select(a => Path.GetDirectoryName(a.AutoSavePath)).Distinct())
            {
                _ = AssetDatabase.DeleteAsset(folder);
            }
        }
    }

    public class AutoSavedSceneRecord
    {
        public AutoSavedSceneRecord(string originalPath, string autoSavePath)
        {
            OriginalPath = originalPath;
            AutoSavePath = autoSavePath;
        }

        public string OriginalPath { get; }
        public string AutoSavePath { get; }
    }
}
