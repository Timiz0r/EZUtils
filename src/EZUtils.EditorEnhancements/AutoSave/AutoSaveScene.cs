namespace EZUtils.EditorEnhancements.AutoSave
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    using static Localization;

    internal class AutoSaveScene
    {
        private readonly ISceneRecoveryRepository sceneRecoveryRepository;

        public Scene Scene { get; }

        public AutoSaveScene(Scene underlyingScene, ISceneRecoveryRepository sceneRecoveryRepository)
        {
            Scene = underlyingScene;
            this.sceneRecoveryRepository = sceneRecoveryRepository;
        }

        public void AutoSave()
        {
            string autoSaveFolderPath = SceneAutoSaver.GetAutoSavePath(Scene.path);
            DirectoryInfo autoSaveFolder = new DirectoryInfo(autoSaveFolderPath);
            string sceneName = string.IsNullOrEmpty(Scene.name) ? "Untitled" : Scene.name;

            if (!autoSaveFolder.Exists) autoSaveFolder.Create();

            //this indeed works even in hidden folders outside the scope of AssetDatabase
            _ = EditorSceneManager.SaveScene(
                Scene,
                $"{autoSaveFolderPath}/{sceneName}-AutoSave-{DateTimeOffset.Now:yyyy'-'MM'-'dd'T'HHmmss}.unity",
                saveAsCopy: true);

            IEnumerable<FileInfo> oldAutoSaves = autoSaveFolder
                .GetFiles("*.unity")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(sceneRecoveryRepository.AutoSaveFileLimit);
            foreach (FileInfo autoSaveFile in oldAutoSaves)
            {
                autoSaveFile.Delete();
            }
        }

        public void ForceRecoverSpecific(FileInfo autoSaveFile)
        {
            const string recoveryAssetPath = "Assets/AutoSaveRecoveryTemp.unity";
            _ = autoSaveFile.CopyTo(recoveryAssetPath);
            AssetDatabase.ImportAsset(recoveryAssetPath, ImportAssetOptions.ForceSynchronousImport);
            Scene backupScene = EditorSceneManager.OpenScene(recoveryAssetPath, OpenSceneMode.Additive);
            try
            {
                string sceneName = string.IsNullOrEmpty(Scene.name) ? "Untitled" : Scene.name;
                using (UndoGroup undoGroup = new UndoGroup(T($"Recover scene '{sceneName}' from auto-save")))
                {
                    foreach (GameObject root in Scene.GetRootGameObjects())
                    {
                        Undo.DestroyObjectImmediate(root);
                    }

                    HashSet<GameObject> addedGameObjects = new HashSet<GameObject>();
                    foreach (GameObject root in backupScene.GetRootGameObjects())
                    {
                        //since we wont save the other scene, moving is perfectly fine and becomes effectively a copy
                        SceneManager.MoveGameObjectToScene(root, Scene);
                        GameObject addedGameObject = Scene
                            .GetRootGameObjects()
                            .Single(go => !addedGameObjects.Contains(go));
                        //the scene-related undo doesnt really seem to work, so we register one of these as well
                        Undo.RegisterCreatedObjectUndo(addedGameObject, T("Copy GameObject"));
                        addedGameObject.transform.SetAsLastSibling();
                        _ = addedGameObjects.Add(addedGameObject);
                    }
                }
            }
            finally
            {
                _ = EditorSceneManager.CloseScene(backupScene, removeScene: true);
                _ = AssetDatabase.DeleteAsset(recoveryAssetPath);
            }

        }

        public void Recover(DateTimeOffset lastCleanTime)
        {
            string autoSaveFolderPath = SceneAutoSaver.GetAutoSavePath(Scene.path);
            DirectoryInfo autoSaveFolder = new DirectoryInfo(autoSaveFolderPath);

            //NOTE: an important implementation details is that we assume all scenes in the folder
            //are viable auto saves, regardless of the name
            //for scene renames, we only move the folder and dont change the file names
            //we leave the files names as potential information to the user as to the history of the scene
            FileInfo latestAutoSaveFile = !autoSaveFolder.Exists
                ? null
                : autoSaveFolder
                    .GetFiles("*.unity")
                    .Where(f => f.CreationTimeUtc > lastCleanTime)
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .FirstOrDefault();
            //so we recovered towards the set of open scenes (via caller), but we can't load an autosave
            if (latestAutoSaveFile == null) return;

            ForceRecoverSpecific(latestAutoSaveFile);
        }
    }
}
