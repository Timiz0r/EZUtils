namespace EZUtils.EditorEnhancements
{
    using System;
    using System.IO;
    using System.Linq;
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;

    //NOTE: we dont store a scene because there are code paths where there is no associated scene,
    //particularly if unity crashed and the initial scenes are different from what was open at the time.
    internal class AutoSaveScene
    {
        private readonly string autoSaveFolderPath;
        private readonly DirectoryInfo autoSaveFolder;
        private readonly string sceneName;
        private readonly bool wasDirty;
        private Scene? cachedScene = null;
        private Scene Scene => cachedScene
            ?? (SceneManager.GetSceneByPath(Path) is Scene scene && scene.IsValid()
                ? (cachedScene = scene).Value
                : default(Scene));

        public string Path { get; }

        public AutoSaveScene(string scenePath, bool wasDirty)
        {
            Path = scenePath;
            this.wasDirty = wasDirty;
            sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            autoSaveFolderPath = string.Concat(
                //a slight preference for unity-style paths means no Path class
                Path.Substring(0, Path.Length - sceneName.Length - ".unity".Length),
                //we use a hidden folder so unity wont do an import each time we create an autosave
                ".SceneAutoSave/",
                sceneName);
            autoSaveFolder = new DirectoryInfo(autoSaveFolderPath);
        }

        public AutoSaveScene(string scenePath) : this(scenePath, wasDirty: false)
        { }

        //which is to say both that an improper close happened, and there is something to recover to
        public bool IsRecoveryNeeded()
        {
            //this happens on domain reload, where scenes remain dirty
            //so nothing has been lost, and nothing needs to be recovered
            if (Scene.isDirty) return false;

            FileInfo sceneFile = new FileInfo(Path);
            //could also go modified time
            //but since these are never intended to be modified, creation time is the best match
            FileInfo latestAutoSave = autoSaveFolder
                .GetFiles("*.unity")
                .OrderByDescending(f => f.CreationTimeUtc)
                .FirstOrDefault();
            if (latestAutoSave == null)
            {
                return false;
            }

            bool sceneNoLongerOpened = !Scene.IsValid();
            bool dirtySceneNoLongerDirty = wasDirty && !Scene.isDirty; //check scene dirtyness again for code maintainability reasons
            //if a scene is copied, it's last write time will be the same as the last save,
            //but it's creation time will be newer
            DateTime sceneModifiedTime = sceneFile.CreationTimeUtc > sceneFile.LastWriteTimeUtc
                ? sceneFile.CreationTimeUtc
                : sceneFile.LastWriteTimeUtc;
            bool autoSaveNewerThanScene = latestAutoSave.CreationTimeUtc > sceneModifiedTime;


            bool isRecoveryNeeded = sceneNoLongerOpened || dirtySceneNoLongerDirty || autoSaveNewerThanScene;
            return isRecoveryNeeded;
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
        }
    }
}
