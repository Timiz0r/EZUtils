namespace EZUtils.EditorEnhancements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;

    using static Localization;

    internal class SceneAutoSaver
    {
        internal static readonly EditorPreference<int> SceneAutoSaveCopies =
            new EditorPreference<int>("EZUtils.EditorEnhancements.AutoSave.Scene.Copies", 5);

        //if unity crashes, the set of scenes opened on load may be different from what were open beforehand
        private readonly EditorPreference<string> rawEditorRecord;
        private readonly EditorRecord editorRecord;

        private readonly Dictionary<string, AutoSaveScene> autoSaveScenes = new Dictionary<string, AutoSaveScene>();

        public SceneAutoSaver()
        {
            rawEditorRecord = new EditorPreference<string>(
                "EZUtils.EditorEnhancements.AutoSave.Scene.EditorRecord", null);
            editorRecord = new EditorRecord();
            if (rawEditorRecord.Value is string r)
            {
                EditorJsonUtility.FromJsonOverwrite(r, editorRecord);
            }
        }

        public void AutoSave()
        {
            foreach (AutoSaveScene scene in autoSaveScenes.Values)
            {
                scene.AutoSave();
            }
        }

        public void Load()
        {
            //it's not normally possible to have zero open scenes
            bool firstTimeLoad = editorRecord.scenes.Count == 0;
            AutoSaveScene[] lastOpenedAutoSaveScenes = editorRecord.scenes
                .Select(s => new AutoSaveScene(s.path, s.wasDirty))
                .ToArray();

            bool improperCloseDetected = SceneManager.sceneCount != lastOpenedAutoSaveScenes.Length
                || lastOpenedAutoSaveScenes.Any(s => s.IsRecoveryNeeded());
            if (!firstTimeLoad
                && improperCloseDetected
                && EditorUtility.DisplayDialog(
                    T("Scene auto-save"),
                    T("It does not appear that Unity was properly closed, and there is auto-save data available. " +
                        "Attempt to recover using auto-save data?"),
                    T("Yes"),
                    T("No")))
            {
                foreach (AutoSaveScene autoSaveScene in lastOpenedAutoSaveScenes)
                {
                    autoSaveScene.Recover();
                }

                //reverse since closing scenes changes the counts and indices
                //we dont use our GetScenes because it goes in ascending order
                for (int i = SceneManager.sceneCount; i >= 0; i--)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    EditorRecord.SceneRecord sceneRecord =
                        editorRecord.scenes.SingleOrDefault(sr => sr.path == scene.path);

                    if (sceneRecord == null)
                    {
                        _ = EditorSceneManager.CloseScene(scene, removeScene: true);
                        continue;
                    }

                    if (sceneRecord.wasActive)
                    {
                        _ = SceneManager.SetActiveScene(scene);
                    }
                    if (!sceneRecord.wasLoaded)
                    {
                        _ = EditorSceneManager.CloseScene(scene, removeScene: false);
                    }
                }

                //we can't set lastOpenedScenePaths directly from the editorpref
                //because if the user doesn't want to recover, we need to populate with the current set of scenes
                foreach (AutoSaveScene scene in lastOpenedAutoSaveScenes)
                {
                    autoSaveScenes[scene.Path] = scene;
                }
            }
            else
            {
                IEnumerable<Scene> initialScenes = Enumerable
                    .Range(0, SceneManager.sceneCount)
                    .Select(i => SceneManager.GetSceneAt(i));
                foreach (Scene scene in initialScenes)
                {
                    autoSaveScenes[scene.path] = new AutoSaveScene(scene.path);
                }
            }

            StoreEditorRecord();

            EditorSceneManager.activeSceneChangedInEditMode += ActiveSceneChanged;
            EditorSceneManager.sceneOpened += SceneOpened;
            EditorSceneManager.sceneClosed += SceneClosed;
        }

        //scene rename notes:
        //GetScenes has two copies of the new name and none of the old name
        //except if there are multiple scenes open, then there's only one of the new name
        //scene is for the new name
        private void SceneClosed(Scene scene)
        {
            //rename
            if (!autoSaveScenes.ContainsKey(scene.path))
            {
                HashSet<string> openedScenes = new HashSet<string>(GetScenes().Select(s => s.path));
                string oldPath = autoSaveScenes.Keys.Single(p => !openedScenes.Contains(p));
                _ = autoSaveScenes.Remove(oldPath);
                _ = editorRecord.scenes.RemoveAll(sr => sr.path == oldPath);
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
            EditorRecord.SceneRecord sceneRecord = new EditorRecord.SceneRecord()
            {
                wasActive = false,
                wasDirty = false,
                wasLoaded = true
            };

            //rename with only one scene opened
            //but because this is a bug and we don't know for sure it only triggers if there's one scene opened,
            //we;ll be extra safe
            if (scene.path == null)
            {
                IEnumerable<string> openedScenes = GetScenes().Select(s => s.path);
                string newPath = openedScenes.Single(p => !autoSaveScenes.ContainsKey(p));

                autoSaveScenes.Add(newPath, new AutoSaveScene(newPath));
                sceneRecord.path = newPath;
            }
            else
            {
                autoSaveScenes.Add(scene.path, new AutoSaveScene(scene.path));
                sceneRecord.path = scene.path;
            }

            editorRecord.scenes.Add(sceneRecord);
            StoreEditorRecord();
        }
        private void ActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            foreach (EditorRecord.SceneRecord sr in editorRecord.scenes)
            {
                sr.wasActive = newScene.path == sr.path;
            }
        }

        private void StoreEditorRecord() => rawEditorRecord.Value = EditorJsonUtility.ToJson(editorRecord);

        private static IEnumerable<Scene> GetScenes() => Enumerable
            .Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i));

        [Serializable]
        private class EditorRecord
        {
            public List<SceneRecord> scenes;

            public class SceneRecord
            {
                public string path;
                public bool wasLoaded;
                public bool wasDirty;
                public bool wasActive;
            }
        }
    }
}
