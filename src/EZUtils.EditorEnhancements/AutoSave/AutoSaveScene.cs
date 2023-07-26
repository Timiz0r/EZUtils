namespace EZUtils.EditorEnhancements
{
    using System;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    using static Localization;

    //NOTE: we dont store a scene because there are code paths where there is no associated scene,
    //particularly if unity crashed and the initial scenes are different from what was open at the time.
    internal class AutoSaveScene
    {
        private readonly string autoSaveFolderPath;
        private readonly DirectoryInfo autoSaveFolder;

        public Scene Scene { get; }

        public string Path => Scene.path;

        public AutoSaveScene(Scene underlyingScene)
        {
            Scene = underlyingScene;
            autoSaveFolderPath = GetAutoSavePath(Path);
            autoSaveFolder = new DirectoryInfo(autoSaveFolderPath);
        }

        public void AutoSave()
        {
            if (!Scene.isDirty) return;

            if (!autoSaveFolder.Exists) autoSaveFolder.Create();

            //this indeed works even in hidden folders outside the scope of AssetDatabase
            _ = EditorSceneManager.SaveScene(
                Scene,
                $"{autoSaveFolderPath}/{Scene.name}-AutoSave-{DateTimeOffset.Now:yyyy'-'MM'-'dd'T'HHmmss}.unity",
                saveAsCopy: true);

            FileInfo[] autoSaveFiles = autoSaveFolder.GetFiles("*.unity");
            if (autoSaveFiles.Length > SceneAutoSaver.SceneAutoSaveCopies.Value)
            {
                autoSaveFiles.OrderBy(f => f.CreationTimeUtc).First().Delete();
            }
        }
        public void Recover()
        {
            FileInfo latestAutoSaveFile = !autoSaveFolder.Exists
                ? null
                : autoSaveFolder
                    .GetFiles("*.unity")
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .FirstOrDefault();
            //so we recovered towards the set of open scenes, but we can't load an autosave
            if (latestAutoSaveFile == null) return;

            Scene backupScene = EditorSceneManager.OpenScene(latestAutoSaveFile.FullName, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in Scene.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                using (UndoGroup undoGroup = new UndoGroup(T($"Recover scene '{Scene.name}' from auto-save")))
                {
                    foreach (GameObject root in backupScene.GetRootGameObjects())
                    {
                        //since we wont save the other scene, moving is perfectly fine and becomes effectively a copy
                        Undo.MoveGameObjectToScene(root, Scene, T("Copy GameObject"));
                    }
                }
            }
            finally
            {
                _ = EditorSceneManager.CloseScene(backupScene, removeScene: true);
            }

        }

        public static string GetAutoSavePath(string scenePath)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            string result = string.Concat(
                //a slight preference for unity-style paths means no Path class
                scenePath.Substring(0, scenePath.Length - sceneName.Length - ".unity".Length),
                //we use a hidden folder so unity wont do an import each time we create an autosave
                ".SceneAutoSave/",
                sceneName);
            return result;
        }
    }
}
