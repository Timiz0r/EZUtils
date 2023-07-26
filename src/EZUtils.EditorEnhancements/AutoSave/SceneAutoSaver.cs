namespace EZUtils.EditorEnhancements
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;

    using static Localization;

    internal partial class SceneAutoSaver
    {
        internal static readonly EditorPreference<int> SceneAutoSaveCopies =
            new EditorPreference<int>("EZUtils.EditorEnhancements.AutoSave.Scene.Copies", 5);

        //if unity crashes, the set of scenes opened on load may be different from what were open beforehand
        private readonly EditorPreference<string> rawEditorRecord = new EditorPreference<string>(
            "EZUtils.EditorEnhancements.AutoSave.Scene.EditorRecord", null);
        private readonly EditorRecord editorRecord = new EditorRecord();

        private readonly Dictionary<string, AutoSaveScene> autoSaveScenes = new Dictionary<string, AutoSaveScene>();

        public void AutoSave()
        {
            foreach (AutoSaveScene scene in autoSaveScenes.Values)
            {
                scene.AutoSave();
            }
        }

        public void Load()
        {
            EditorRecord previousEditorRecord = new EditorRecord();
            if (rawEditorRecord.Value is string r)
            {
                EditorJsonUtility.FromJsonOverwrite(r, previousEditorRecord);
            }

            if (previousEditorRecord.scenes.Any(sr => IsRecoveryNeeded(sr))
                && EditorUtility.DisplayDialog(
                    T("Scene auto-save"),
                    T("It does not appear that Unity was properly closed, and there is auto-save data available. " +
                        "Attempt to recover using auto-save data?"),
                    T("Yes"),
                    T("No")))
            {
                foreach (EditorSceneRecord sceneRecord in previousEditorRecord.scenes)
                {
                    Scene scene = SceneManager.GetSceneByPath(sceneRecord.path);
                    if (!scene.IsValid())
                    {
                        scene = EditorSceneManager.OpenScene(sceneRecord.path, OpenSceneMode.Additive);
                    }

                    AutoSaveScene autoSaveScene = new AutoSaveScene(scene);
                    autoSaveScene.Recover();

                    if (sceneRecord.wasActive)
                    {
                        _ = SceneManager.SetActiveScene(scene);
                    }
                    if (!sceneRecord.wasLoaded)
                    {
                        _ = EditorSceneManager.CloseScene(scene, removeScene: false);
                    }

                    autoSaveScenes.Add(sceneRecord.path, autoSaveScene);
                }

                //reverse since closing scenes changes the counts and indices
                //we dont use our GetScenes because it goes in ascending order
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    EditorSceneRecord sceneRecord =
                        previousEditorRecord.scenes.SingleOrDefault(sr => sr.path == scene.path);

                    if (sceneRecord == null)
                    {
                        _ = EditorSceneManager.CloseScene(scene, removeScene: true);
                    }
                }
            }
            else
            {
                IEnumerable<Scene> initialScenes = Enumerable
                    .Range(0, SceneManager.sceneCount)
                    .Select(i => SceneManager.GetSceneAt(i));
                foreach (Scene scene in initialScenes)
                {
                    autoSaveScenes.Add(scene.path, new AutoSaveScene(scene));
                }
            }

            Scene activeScene = SceneManager.GetActiveScene();
            foreach (AutoSaveScene scene in autoSaveScenes.Values)
            {
                editorRecord.scenes.Add(new EditorSceneRecord()
                {
                    path = scene.Path,
                    wasActive = scene.Path == activeScene.path,
                    wasDirty = scene.Scene.isDirty,
                    wasLoaded = scene.Scene.isLoaded,
                    lastCleanTime = DateTimeOffset.Now
                });
            }
            StoreEditorRecord();

            EditorSceneManager.activeSceneChangedInEditMode += ActiveSceneChanged;
            EditorSceneManager.sceneOpened += SceneOpened;
            EditorSceneManager.sceneClosed += SceneClosed;
            EditorSceneManager.sceneDirtied += SceneDirtied;
            EditorSceneManager.sceneSaved += SceneSaved;
            Undo.undoRedoPerformed += UndoRedo;
            EditorApplication.quitting += EditorQuitting;
        }

        private void EditorQuitting()
        {
            //clean quits means we dont need to keep around this state to recover
            editorRecord.scenes.Clear();
            StoreEditorRecord();
        }

        //there are no scene-related events for when a scene is undid back into a clean state
        private void UndoRedo()
        {
            foreach (AutoSaveScene scene in autoSaveScenes.Values)
            {
                editorRecord.scenes.Single(sr => sr.path == scene.Path).SetDirtiness(scene.Scene.isDirty);
            }
            StoreEditorRecord();
        }

        private void SceneSaved(Scene scene)
        {
            editorRecord.scenes.Single(sr => sr.path == scene.path).SetDirtiness(scene.isDirty);
            StoreEditorRecord();
        }

        private void SceneDirtied(Scene scene)
        {
            editorRecord.scenes.Single(sr => sr.path == scene.path).SetDirtiness(scene.isDirty);
            StoreEditorRecord();
        }

        //scene rename notes:
        //GetScenes has two copies of the new name and none of the old name
        //except if there are multiple scenes open, then there's only one of the new name
        //scene is for the new name
        private void SceneClosed(Scene scene)
        {
            string targetScenePath;

            //rename
            if (!autoSaveScenes.ContainsKey(scene.path))
            {
                HashSet<string> openedScenes = new HashSet<string>(GetScenes().Select(s => s.path));
                targetScenePath = autoSaveScenes.Keys.Single(p => !openedScenes.Contains(p));
            }
            else
            {
                _ = autoSaveScenes.Remove(scene.path);
                _ = editorRecord.scenes.RemoveAll(sr => sr.path == scene.path);
            }

            StoreEditorRecord();
        }

        //scene rename notes:
        //GetScenes has a copy of the new name and none of the old name
        //scene is default and useless (actually forgot to check if default or just weird)
        //except if there are multiple scenes, then it's the new scene
        private void SceneOpened(Scene scene, OpenSceneMode mode)
        {
            //rename with only one scene opened
            //but because this is a bug and we don't know for sure it only triggers if there's one scene opened,
            //we;ll be extra safe
            if (!scene.IsValid())
            {
                IEnumerable<string> openedScenes = GetScenes().Select(s => s.path);
                string newPath = openedScenes.Single(p => !autoSaveScenes.ContainsKey(p));

                scene = SceneManager.GetSceneByPath(newPath);
            }

            autoSaveScenes.Add(scene.path, new AutoSaveScene(scene));
            editorRecord.scenes.Add(new EditorSceneRecord
            {
                wasLoaded = mode != OpenSceneMode.AdditiveWithoutLoading,
                path = scene.path,
                lastCleanTime = DateTimeOffset.Now
            });
            StoreEditorRecord();
        }

        private void ActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            foreach (EditorSceneRecord sr in editorRecord.scenes)
            {
                sr.wasActive = newScene.path == sr.path;
            }
            StoreEditorRecord();
        }

        private void StoreEditorRecord() => rawEditorRecord.Value = EditorJsonUtility.ToJson(editorRecord);

        //which is to say both that an improper close happened, and there is something to recover to 
        private static bool IsRecoveryNeeded(EditorSceneRecord sceneRecord)
        {
            Scene scene = SceneManager.GetSceneByPath(sceneRecord.path);

            //this happens on domain reload, where scenes remain dirty
            //so nothing has been lost, and nothing needs to be recovered
            if (scene.IsValid() && scene.isDirty) return false;

            //could also go modified time
            //but since these are never intended to be modified, creation time is the best match
            FileInfo latestAutoSave = new DirectoryInfo(AutoSaveScene.GetAutoSavePath(sceneRecord.path))
                .GetFiles("*.unity")
                .OrderByDescending(f => f.CreationTimeUtc)
                .FirstOrDefault();
            if (latestAutoSave == null)
            {
                return false;
            }

            bool sceneNoLongerOpened = !scene.IsValid();
            bool dirtySceneNoLongerDirty = sceneRecord.wasDirty && !scene.isDirty;
            bool autoSaveNewerThanLastCleanTime = latestAutoSave.CreationTimeUtc > sceneRecord.lastCleanTime;

            bool isRecoveryNeeded = sceneNoLongerOpened || dirtySceneNoLongerDirty || autoSaveNewerThanLastCleanTime;
            return isRecoveryNeeded;
        }

        private static IEnumerable<Scene> GetScenes() => Enumerable
            .Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i));

        [Serializable]
        private class EditorRecord
        {
            public List<EditorSceneRecord> scenes = new List<EditorSceneRecord>();
        }
    }
}
