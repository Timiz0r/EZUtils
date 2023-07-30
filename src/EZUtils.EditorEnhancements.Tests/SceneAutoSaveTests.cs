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

    //these are technically integration tests since they are stongly coupled to certain unity things like scenes, assets
    //but we make the tests and the design as unit-testy as possible
    //we want integration tests because it's the behavior in unity we care about
    //and it's not worth the design effort to get the design to be purely unit testable
    public class SceneAutoSaveTests
    {
        [OneTimeSetUp]
        public static void OneTimeSetup() => AutoSave.Disable();

        [OneTimeTearDown]
        public void OneTimeTeardown()
        {
            AutoSave.Enable();
        }

        [TearDown]
        public void Teardown()
        {
            _ = AssetDatabase.DeleteAsset(TestScene.TestSceneRootPath);
        }

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

        [Test]
        public void AutoSave_RecoversFromAutoSave_WhenSceneAlreadyOpenOnRestartFromCrash()
        {
            using (TestSceneStateRepository sceneRepository = new TestSceneStateRepository())
            {
                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    sceneAutoSaver.Load();

                    _ = new GameObject("test");
                    testScene.MarkDirty();
                    sceneAutoSaver.AutoSave();

                    sceneRepository.SimulateUnityCrash();
                }

                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    Assert.That(testScene.Scene.rootCount, Is.EqualTo(0));

                    sceneAutoSaver.Load();

                    Assert.That(testScene.Scene.rootCount, Is.EqualTo(1));
                    Assert.That(testScene.Scene.isDirty, Is.EqualTo(true));
                }
            }
        }

        [Test]
        public void AutoSave_RecoversFromAutoSave_WhenDifferentSceneOpenOnRestartFromCrash()
        {
            using (TestSceneStateRepository sceneRepository = new TestSceneStateRepository())
            {
                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    sceneAutoSaver.Load();

                    _ = new GameObject("test");
                    testScene.MarkDirty();
                    sceneAutoSaver.AutoSave();

                    sceneRepository.SimulateUnityCrash();
                }

                using (TestScene testScene2 = new TestScene("testscene2"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    Assert.That(testScene2.Scene.rootCount, Is.EqualTo(0));

                    sceneAutoSaver.Load();
                    Assert.That(testScene2.IsOpen, Is.False);

                    using (TestScene testScene = new TestScene("testscene", mustAlreadyBeOpen: true))
                    {
                        Assert.That(testScene.Scene.rootCount, Is.EqualTo(1));
                        Assert.That(testScene.Scene.isDirty, Is.EqualTo(true));
                    }
                }
            }
        }

        [Test]
        public void AutoSave_DoesNotAttemptRecovery_WhenUnityClosedNormally()
        {
            using (TestSceneStateRepository sceneRepository = new TestSceneStateRepository())
            {
                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    sceneAutoSaver.Load();

                    _ = new GameObject("test");
                    testScene.MarkDirty();
                    sceneAutoSaver.AutoSave();

                    sceneRepository.SimulateUnityClose();
                }

                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    Assert.That(testScene.Scene.rootCount, Is.EqualTo(0));

                    sceneAutoSaver.Load();

                    Assert.That(testScene.Scene.rootCount, Is.EqualTo(0));
                    Assert.That(testScene.Scene.isDirty, Is.EqualTo(false));
                }
            }
        }

        [Test]
        public void AutoSave_DoesNotAttemptRecovery_WhenUnityCrashesBeforeAutoSave()
        {
            using (TestSceneStateRepository sceneRepository = new TestSceneStateRepository())
            {
                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    sceneAutoSaver.Load();

                    _ = new GameObject("test");
                    testScene.MarkDirty();

                    sceneRepository.SimulateUnityCrash();
                }

                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    Assert.That(testScene.Scene.rootCount, Is.EqualTo(0));

                    sceneAutoSaver.Load();

                    Assert.That(testScene.Scene.rootCount, Is.EqualTo(0));
                    Assert.That(testScene.Scene.isDirty, Is.EqualTo(false));
                }
            }
        }

        [Test]
        public void AutoSave_DoesNotAttemptRecovery_WhenSceneSavedProperly()
        {
            using (TestSceneStateRepository sceneRepository = new TestSceneStateRepository())
            {
                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    sceneAutoSaver.Load();

                    _ = new GameObject("test");
                    testScene.MarkDirty();
                    sceneAutoSaver.AutoSave();

                    _ = new GameObject("test2");
                    testScene.Save();

                    sceneRepository.SimulateUnityCrash();
                }

                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    Assert.That(testScene.Scene.rootCount, Is.EqualTo(2));

                    sceneAutoSaver.Load();

                    Assert.That(testScene.Scene.rootCount, Is.EqualTo(2));
                    Assert.That(testScene.Scene.isDirty, Is.EqualTo(false));
                }
            }
        }

        [Test]
        public void AutoSave_DoesNotAttemptRecovery_WhenAutoSaveIsOlderThanCleanTime()
        {
            using (TestSceneStateRepository sceneRepository = new TestSceneStateRepository())
            {
                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    sceneAutoSaver.Load();

                    _ = new GameObject("test");
                    testScene.MarkDirty();
                    sceneAutoSaver.AutoSave();

                    _ = new GameObject("test2");
                    testScene.Save();

                    _ = new GameObject("test3");
                    testScene.MarkDirty();

                    sceneRepository.SimulateUnityCrash();
                }

                //to summarize, the old autosave has 1 root
                //then we save with 2 roots and should no longer use that autosave for being too old
                //then we dirty the scene and crash, so, aside from not having a new-enough autosave,
                //we're eligible to recover

                using (TestScene testScene = new TestScene("testscene"))
                using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
                {
                    Assert.That(testScene.Scene.rootCount, Is.EqualTo(2));

                    sceneAutoSaver.Load();

                    Assert.That(testScene.Scene.rootCount, Is.EqualTo(2));
                    Assert.That(testScene.Scene.isDirty, Is.EqualTo(false));
                }
            }
        }
    }

    public class TestScene : IDisposable
    {
        public static readonly string TestSceneRootPath = "Assets/SceneAutoSaveTests";

        private readonly string scenePath;

        public TestScene(string sceneName, bool mustAlreadyBeOpen = false)
        {
            scenePath = $"{TestSceneRootPath}/{sceneName}.unity";

            Scene = GetScenes().SingleOrDefault(s => s.path == scenePath);

            if (!Scene.IsValid())
            {
                Assert.That(
                    mustAlreadyBeOpen,
                    Is.False,
                    $"Test scene '{sceneName}' was expected to already be open but was not.");

                if (File.Exists(scenePath))
                {
                    Scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
                else
                {
                    Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    _ = Directory.CreateDirectory(TestSceneRootPath);
                    _ = EditorSceneManager.SaveScene(Scene, scenePath);
                }
            }
        }

        public Scene Scene { get; }
        public bool IsOpen => GetScenes().Any(s => s.path == Scene.path);

        public void MarkDirty() => EditorSceneManager.MarkSceneDirty(Scene);
        public void Save() => EditorSceneManager.SaveScene(Scene);

        public void Dispose() =>
            //_ = EditorSceneManager.CloseScene(Scene, removeScene: true);
            //since we opened it single, we need another scene in order to close
            _ = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        private static IEnumerable<Scene> GetScenes() => Enumerable
            .Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i));
    }

    public class TestSceneStateRepository : ISceneRecoveryRepository, IDisposable
    {
        private readonly List<AutoSavedSceneRecord> autoSavedScenes = new List<AutoSavedSceneRecord>();
        private bool mayPerformRecovery = false;

        public TestSceneStateRepository()
        {
            EditorSceneManager.sceneSaved += SceneSaved;
        }

        public IReadOnlyList<EditorSceneRecord> Scenes { get; private set; } = new List<EditorSceneRecord>();

        public IReadOnlyList<EditorSceneRecord> RecoverScenes() => Scenes;

        public void UpdateScenes(IEnumerable<EditorSceneRecord> sceneRecords) => Scenes = sceneRecords.ToArray();

        public bool MayPerformRecovery() => mayPerformRecovery;

        public int GetAutoSaveCount(Scene scene) => autoSavedScenes.Count(a => a.OriginalPath == scene.path);

        public void SimulateUnityClose()
        {
            UpdateScenes(Enumerable.Empty<EditorSceneRecord>());
            mayPerformRecovery = false;
        }

        public void SimulateUnityCrash() => mayPerformRecovery = true;

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
