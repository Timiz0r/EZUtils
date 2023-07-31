namespace EZUtils.EditorEnhancements.AutoSave.Tests
{
    using System.IO;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    //these are technically integration tests since they are stongly coupled to certain unity things like scenes, assets
    //but we make the tests and the design as unit-testy as possible
    //we want integration tests because it's the behavior in unity we care about
    //and it's not worth the design effort to get the design to be purely unit testable
    public class SceneAutoSaveTests
    {
        [OneTimeSetUp]
        public static void OneTimeSetup()
        {
            //a small consequence of the integration testness is that we'll delete untitled autosaves, even if legit
            //luckily for this repo, we dont expect to work in them, so it's not a problem
            _ = AssetDatabase.DeleteAsset(SceneAutoSaver.GetAutoSavePath(string.Empty));
            AutoSave.Disable();
        }

        [OneTimeTearDown]
        public void OneTimeTeardown() => AutoSave.Enable();

        [TearDown]
        public void Teardown()
        {
            _ = AssetDatabase.DeleteAsset(TestScene.TestSceneRootPath);
            _ = AssetDatabase.DeleteAsset(SceneAutoSaver.GetAutoSavePath(string.Empty));
        }

        [Test]
        public void AutoSave_DoesNotSave_WhenSceneNotDirty()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            using (TestScene testScene = new TestScene("testscene"))
            {
                sceneAutoSaver.Load();

                sceneAutoSaver.AutoSave();

                Assert.That(sceneRepository.GetAvailableAutoSaveCount(testScene.Scene), Is.EqualTo(0));
            }
        }

        [Test]
        public void AutoSave_Saves_WhenSceneDirty()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            using (TestScene testScene = new TestScene("testscene"))
            {
                sceneAutoSaver.Load();

                _ = new GameObject("test");
                testScene.MarkDirty();
                sceneAutoSaver.AutoSave();

                Assert.That(sceneRepository.GetAvailableAutoSaveCount(testScene.Scene), Is.EqualTo(1));
            }
        }

        [Test]
        public void AutoSave_RecoversFromAutoSave_WhenSceneAlreadyOpenOnRestartFromCrash()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
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

        [Test]
        public void AutoSave_RecoversFromAutoSave_WhenDifferentSceneOpenOnRestartFromCrash()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
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

        [Test]
        public void AutoSave_DoesNotAttemptRecovery_WhenUnityClosedNormally()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
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

        [Test]
        public void AutoSave_DoesNotAttemptRecovery_WhenUnityCrashesBeforeAutoSave()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
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

        [Test]
        public void AutoSave_DoesNotAttemptRecovery_WhenSceneSavedProperly()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
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

        [Test]
        public void AutoSave_DoesNotAttemptRecovery_WhenAutoSaveIsOlderThanCleanTime()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
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

        [Test]
        public void AutoSave_AttemptsMultiSceneRecovery_WhenOnlyOneSceneNeedsRecoveryAndOtherHasExpiredAutoSave()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            using (TestScene testScene = new TestScene("testscene"))
            using (TestScene testScene2 = new TestScene("testscene2", additive: true))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                sceneAutoSaver.Load();

                testScene.MakeActive();
                _ = new GameObject("test");
                testScene.MarkDirty();

                testScene2.MakeActive();
                _ = new GameObject("test");
                testScene2.MarkDirty();

                sceneAutoSaver.AutoSave();

                _ = new GameObject("test2");
                testScene2.Save();

                sceneRepository.SimulateUnityCrash();
            }

            //so testscene has an autosave and is still dirty when crash happens
            //and testscene2 has old autosave but is clean when crash happens

            using (TestScene testScene = new TestScene("testscene"))
            using (TestScene testScene2 = new TestScene("testscene2", additive: true))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                Assert.That(testScene.Scene.rootCount, Is.EqualTo(0));
                Assert.That(testScene2.Scene.rootCount, Is.EqualTo(2));

                sceneAutoSaver.Load();

                Assert.That(testScene.Scene.rootCount, Is.EqualTo(1));
                Assert.That(testScene.Scene.isDirty, Is.EqualTo(true));
                Assert.That(testScene2.Scene.rootCount, Is.EqualTo(2));
                Assert.That(testScene2.Scene.isDirty, Is.EqualTo(false));
            }
        }

        [Test]
        public void AutoSave_AttemptsMultiSceneRecovery_WhenAllScenesAlreadyOpen()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            using (TestScene testScene = new TestScene("testscene"))
            using (TestScene testScene2 = new TestScene("testscene2", additive: true))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                sceneAutoSaver.Load();

                testScene.MakeActive();
                _ = new GameObject("test");
                testScene.MarkDirty();

                testScene2.MakeActive();
                _ = new GameObject("test");
                testScene2.MarkDirty();

                sceneAutoSaver.AutoSave();

                sceneRepository.SimulateUnityCrash();
            }

            using (TestScene testScene = new TestScene("testscene"))
            using (TestScene testScene2 = new TestScene("testscene2", additive: true))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                Assert.That(testScene.Scene.rootCount, Is.EqualTo(0));
                Assert.That(testScene2.Scene.rootCount, Is.EqualTo(0));

                sceneAutoSaver.Load();

                Assert.That(testScene.Scene.rootCount, Is.EqualTo(1));
                Assert.That(testScene.Scene.isDirty, Is.EqualTo(true));
                Assert.That(testScene2.Scene.rootCount, Is.EqualTo(1));
                Assert.That(testScene2.Scene.isDirty, Is.EqualTo(true));
            }
        }

        [Test]
        public void AutoSave_AttemptsMultiSceneRecovery_WhenOneSceneIsNotOpen()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            using (TestScene testScene = new TestScene("testscene"))
            using (TestScene testScene2 = new TestScene("testscene2", additive: true))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                sceneAutoSaver.Load();

                testScene.MakeActive();
                _ = new GameObject("test");
                testScene.MarkDirty();

                testScene2.MakeActive();
                _ = new GameObject("test");
                testScene2.MarkDirty();

                sceneAutoSaver.AutoSave();

                sceneRepository.SimulateUnityCrash();
            }

            using (TestScene testScene = new TestScene("testscene"))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                Assert.That(testScene.Scene.rootCount, Is.EqualTo(0));
                using (TestScene testScene2 = new TestScene("testscene2", additive: true))
                {
                    Assert.That(testScene2.Scene.rootCount, Is.EqualTo(0));
                }

                sceneAutoSaver.Load();

                Assert.That(testScene.Scene.rootCount, Is.EqualTo(1));
                Assert.That(testScene.Scene.isDirty, Is.EqualTo(true));
                using (TestScene testScene2 = new TestScene("testscene2", mustAlreadyBeOpen: true))
                {
                    Assert.That(testScene2.Scene.rootCount, Is.EqualTo(1));
                    Assert.That(testScene2.Scene.isDirty, Is.EqualTo(true));
                }
            }
        }

        [Test]
        public void AutoSave_AttemptsMultiSceneRecovery_WhenOneSceneNotLoaded()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            using (TestScene testScene = new TestScene("testscene"))
            using (TestScene testScene2 = new TestScene("testscene2", additive: true))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                sceneAutoSaver.Load();

                testScene.MakeActive();
                _ = new GameObject("test");
                testScene.MarkDirty();

                testScene2.MakeActive();
                _ = new GameObject("test");
                testScene2.MarkDirty();

                sceneAutoSaver.AutoSave();

                testScene2.Unload();

                sceneRepository.SimulateUnityCrash();
            }

            using (TestScene testScene = new TestScene("testscene"))
            using (TestScene testScene2 = new TestScene("testscene2", additive: true))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                Assert.That(testScene.Scene.rootCount, Is.EqualTo(0));
                Assert.That(testScene2.Scene.rootCount, Is.EqualTo(0));
                Assert.That(testScene2.Scene.isLoaded, Is.True);

                sceneAutoSaver.Load();

                Assert.That(testScene.Scene.rootCount, Is.EqualTo(1));
                Assert.That(testScene.Scene.isDirty, Is.EqualTo(true));
                Assert.That(testScene2.Scene.isLoaded, Is.False);
            }
        }

        [Test]
        public void AutoSave_RecoversFromAutoSave_WhenSceneUntitled()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                sceneAutoSaver.Load();

                Scene untitledScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

                _ = new GameObject("test");
                _ = EditorSceneManager.MarkSceneDirty(untitledScene);

                sceneAutoSaver.AutoSave();

                sceneRepository.SimulateUnityCrash();
            }

            Scene restartUntitledScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                sceneAutoSaver.Load();

                Assert.That(restartUntitledScene.rootCount, Is.EqualTo(3));
                Assert.That(restartUntitledScene.isDirty, Is.EqualTo(true));
            }
        }

        [Test]
        public void AutoSave_RecoversFromAutoSave_WhenSceneAssetDeleted()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            string originalScenePath;

            using (TestScene testScene = new TestScene("testscene"))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                originalScenePath = testScene.Scene.path;
                sceneAutoSaver.Load();

                _ = new GameObject("test");
                testScene.MarkDirty();
                sceneAutoSaver.AutoSave();

                _ = AssetDatabase.DeleteAsset(testScene.Scene.path);

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
                    Assert.That(testScene.Scene.path, Is.EqualTo(originalScenePath));
                    Assert.That(File.Exists(testScene.Scene.path), Is.False);
                }
            }
        }

        //there appears to be a unity bug where moving a scene asset causes future attempts to open a scene of the same path
        //to either use some now-invalid cached version, or simply have no root objects
        //as such, for move-related tests, we make sure to use very unique scene names
        [Test]
        public void AutoSave_ChangesAutoSaveLocation_WhenSceneMoved()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            using (TestScene testScene = new TestScene($"testscene-{nameof(AutoSave_ChangesAutoSaveLocation_WhenSceneMoved)}"))
            {
                sceneAutoSaver.Load();

                _ = new GameObject("test");
                testScene.MarkDirty();
                sceneAutoSaver.AutoSave();

                testScene.Move($"{TestScene.TestSceneRootPath}/testscene2-{nameof(AutoSave_ChangesAutoSaveLocation_WhenSceneMoved)}.unity");
                Assert.That(sceneRepository.GetAvailableAutoSaveCount(testScene.Scene), Is.EqualTo(1));

                sceneAutoSaver.AutoSave();
                Assert.That(sceneRepository.GetAvailableAutoSaveCount(testScene.Scene), Is.EqualTo(2));
            }
        }

        [Test]
        public void AutoSave_MergesAutoSaveFolders_WhenSceneMoved()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            using (TestScene testScene = new TestScene($"testscene-{nameof(AutoSave_MergesAutoSaveFolders_WhenSceneMoved)}"))
            {
                sceneAutoSaver.Load();

                _ = new GameObject("test");
                testScene.MarkDirty();
                sceneAutoSaver.AutoSave();

                using (TestScene testScene2 = new TestScene(
                    $"testscene2-{nameof(AutoSave_MergesAutoSaveFolders_WhenSceneMoved)}", additive: true))
                {
                    testScene2.MakeActive();
                    _ = new GameObject("test");
                    testScene2.MarkDirty();
                    sceneAutoSaver.AutoSave();
                    _ = AssetDatabase.DeleteAsset(testScene2.Scene.path);
                }

                testScene.Move($"{TestScene.TestSceneRootPath}/testscene2-{nameof(AutoSave_MergesAutoSaveFolders_WhenSceneMoved)}.unity");
                Assert.That(sceneRepository.GetAvailableAutoSaveCount(testScene.Scene), Is.EqualTo(2));
            }
        }

        [Test]
        public void AutoSave_RecoversFromAutoSave_WhenSceneMoved()
        {
            TestSceneRecoveryRepository sceneRepository = new TestSceneRecoveryRepository();
            using (TestScene testScene = new TestScene($"testscene-{nameof(AutoSave_RecoversFromAutoSave_WhenSceneMoved)}"))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                sceneAutoSaver.Load();

                _ = new GameObject("test");
                testScene.MarkDirty();
                sceneAutoSaver.AutoSave();

                testScene.Move($"{TestScene.TestSceneRootPath}/testscene2-{nameof(AutoSave_RecoversFromAutoSave_WhenSceneMoved)}.unity");

                sceneRepository.SimulateUnityCrash();
            }

            using (TestScene testScene = new TestScene($"testscene2-{nameof(AutoSave_RecoversFromAutoSave_WhenSceneMoved)}"))
            using (SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(sceneRepository))
            {
                Assert.That(testScene.Scene.rootCount, Is.EqualTo(0));

                sceneAutoSaver.Load();

                Assert.That(testScene.Scene.rootCount, Is.EqualTo(1));
                Assert.That(testScene.Scene.isDirty, Is.EqualTo(true));
            }
        }
    }
}
