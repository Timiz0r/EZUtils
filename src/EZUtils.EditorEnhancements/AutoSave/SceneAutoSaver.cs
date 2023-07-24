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
        private readonly EditorPreference<string> lastOpenScenes =
            new EditorPreference<string>("EZUtils.EditorEnhancements.AutoSave.Scene.LastOpen", null);
        private readonly EditorPreference<string> activeScene =
            new EditorPreference<string>("EZUtils.EditorEnhancements.AutoSave.Scene.Active", string.Empty);

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
            bool firstTimeLoad = lastOpenScenes.Value == null;
            HashSet<string> lastOpenedScenePaths = firstTimeLoad
                ? new HashSet<string>()
                : new HashSet<string>(
                    lastOpenScenes.Value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            AutoSaveScene[] lastOpenedAutoSaveScenes = lastOpenedScenePaths
                .Select(p => new AutoSaveScene(p))
                .ToArray();

            bool improperCloseDetected = SceneManager.sceneCount != lastOpenedAutoSaveScenes.Length
                || lastOpenedAutoSaveScenes.Any(s => s.IsAutoSaveNewer());
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
                string lastActiveScene = activeScene.Value;
                for (int i = SceneManager.sceneCount; i >= 0; i--)
                {
                    Scene scene = SceneManager.GetSceneAt(i);

                    if (!lastOpenedScenePaths.Contains(scene.path))
                    {
                        _ = EditorSceneManager.CloseScene(scene, removeScene: true);
                    }

                    if (lastActiveScene == scene.path)
                    {
                        _ = SceneManager.SetActiveScene(scene);
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

            UpdateLastOpenedScenes();

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
            }
            else
            {
                _ = autoSaveScenes.Remove(scene.path);
            }

            UpdateLastOpenedScenes();
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
            if (scene.path == null)
            {
                IEnumerable<string> openedScenes = GetScenes().Select(s => s.path);
                string newPath = openedScenes.Single(p => !autoSaveScenes.ContainsKey(p));
                autoSaveScenes.Add(newPath, new AutoSaveScene(newPath));
            }
            else
            {
                autoSaveScenes.Add(scene.path, new AutoSaveScene(scene.path));
            }

            UpdateLastOpenedScenes();
        }
        private void ActiveSceneChanged(Scene oldScene, Scene newScene) => activeScene.Value = newScene.path;
        private void UpdateLastOpenedScenes() => lastOpenScenes.Value = string.Join(";", autoSaveScenes.Keys);

        private IEnumerable<Scene> GetScenes() => Enumerable
            .Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i));
    }
}
