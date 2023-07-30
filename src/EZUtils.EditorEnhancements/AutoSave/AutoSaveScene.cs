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

    internal class AutoSaveScene
    {
        private readonly string autoSaveFolderPath;
        private readonly DirectoryInfo autoSaveFolder;
        private readonly string sceneName;

        public Scene Scene { get; }

        public AutoSaveScene(Scene underlyingScene)
        {
            Scene = underlyingScene;

            autoSaveFolderPath = SceneAutoSaver.GetAutoSavePath(Scene.path);
            autoSaveFolder = new DirectoryInfo(autoSaveFolderPath);
            sceneName = string.IsNullOrEmpty(Scene.name) ? "Untitled" : underlyingScene.name;
        }

        public void AutoSave()
        {
            if (!Scene.isDirty) return;

            if (!autoSaveFolder.Exists) autoSaveFolder.Create();

            //this indeed works even in hidden folders outside the scope of AssetDatabase
            _ = EditorSceneManager.SaveScene(
                Scene,
                $"{autoSaveFolderPath}/{sceneName}-AutoSave-{DateTimeOffset.Now:yyyy'-'MM'-'dd'T'HHmmss}.unity",
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

            const string recoveryAssetPath = "Assets/AutoSaveRecoveryTemp.unity";
            _ = latestAutoSaveFile.CopyTo(recoveryAssetPath);
            AssetDatabase.ImportAsset(recoveryAssetPath, ImportAssetOptions.ForceSynchronousImport);
            Scene backupScene = EditorSceneManager.OpenScene(recoveryAssetPath, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in Scene.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                using (UndoGroup undoGroup = new UndoGroup(T($"Recover scene '{sceneName}' from auto-save")))
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
                _ = AssetDatabase.DeleteAsset(recoveryAssetPath);
            }

        }
    }
}
