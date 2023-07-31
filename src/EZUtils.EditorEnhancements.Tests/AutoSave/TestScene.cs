namespace EZUtils.EditorEnhancements.AutoSave.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;

    public class TestScene : IDisposable
    {
        public static readonly string TestSceneRootPath = "Assets/SceneAutoSaveTests";

        private readonly string scenePath;

        public TestScene(
            string sceneName,
            bool mustAlreadyBeOpen = false,
            bool additive = false,
            bool createWithDefaultObjects = false)
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
                    Scene = EditorSceneManager.OpenScene(
                        scenePath,
                        additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
                }
                else
                {
                    Scene = EditorSceneManager.NewScene(
                        createWithDefaultObjects ? NewSceneSetup.DefaultGameObjects : NewSceneSetup.EmptyScene,
                        additive ? NewSceneMode.Additive : NewSceneMode.Single);
                    _ = Directory.CreateDirectory(TestSceneRootPath);
                    _ = EditorSceneManager.SaveScene(Scene, scenePath);
                }
            }
        }

        public Scene Scene { get; private set; }
        public bool IsOpen => GetScenes().Any(s => s.path == Scene.path);

        public void MarkDirty() => EditorSceneManager.MarkSceneDirty(Scene);
        public void Save() => EditorSceneManager.SaveScene(Scene);
        public void MakeActive() => SceneManager.SetActiveScene(Scene);
        public void Load() => EditorSceneManager.OpenScene(Scene.path, OpenSceneMode.Additive);
        public void Unload() => EditorSceneManager.CloseScene(Scene, removeScene: false);
        public void Move(string newPath)
        {
            string oldPath = Scene.path;
            _ = AssetDatabase.MoveAsset(Scene.path, newPath);
        }

        public void Dispose()
        {
            if (GetScenes().Count() > 1)
            {
                _ = EditorSceneManager.CloseScene(Scene, removeScene: true);
            }
            //since we opened it single, we need another scene in order to close
            else
            {
                _ = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }

        private static IEnumerable<Scene> GetScenes() => Enumerable
            .Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i));
    }
}
