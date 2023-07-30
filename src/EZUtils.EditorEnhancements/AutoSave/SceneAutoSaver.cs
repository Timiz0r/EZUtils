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

    public class SceneAutoSaver : IDisposable
    {
        internal static readonly EditorPreference<int> SceneAutoSaveCopies =
            new EditorPreference<int>("EZUtils.EditorEnhancements.AutoSave.Scene.Copies", 5);

        private readonly Dictionary<string, AutoSaveScene> autoSaveScenes = new Dictionary<string, AutoSaveScene>();
        private readonly List<EditorSceneRecord> sceneRecords = new List<EditorSceneRecord>();
        private readonly ISceneStateRepository sceneStateRepository;

        public SceneAutoSaver(ISceneStateRepository sceneStateRepository)
        {
            this.sceneStateRepository = sceneStateRepository;
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
            IReadOnlyList<EditorSceneRecord> recoveredScenes = sceneStateRepository.RecoverScenes();

            //TODO: a quirk/bug is that not all scenes that were tracked necessarily have an auto-save
            //which would happen if a new scene was opened/created between auto-saves
            //we should theoretically make sure recover only recovers using the latest file past the checkpoint
            //furthermore, for untitled scenes, we should generate an auto-save as soon as a scene is created
            //or perhaps delay-called, because, if there's no auto-save to recover from, we at least want the initial
            //creation objects to be present
            if (recoveredScenes.Any(sr => IsRecoveryNeeded(sr))
                && EditorUtility.DisplayDialog(
                    T("Scene auto-save"),
                    T("It does not appear that Unity was properly closed, but there is auto-save data available. " +
                        "Attempt to recover using auto-save data?"),
                    T("Recover"),
                    T("Do not recover")))
            {
                foreach (EditorSceneRecord sceneRecord in recoveredScenes)
                {
                    Scene scene = sceneRecord.path.Length == 0
                        ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive)
                        : SceneManager.GetSceneByPath(sceneRecord.path);
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
                    EditorSceneRecord sceneRecord = recoveredScenes.SingleOrDefault(sr => sr.path == scene.path);

                    if (sceneRecord == null)
                    {
                        _ = EditorSceneManager.CloseScene(scene, removeScene: true);
                    }
                }
            }
            else
            {
                foreach (Scene scene in GetScenes())
                {
                    autoSaveScenes.Add(scene.path, new AutoSaveScene(scene));
                }
            }

            Scene activeScene = SceneManager.GetActiveScene();
            foreach (Scene scene in GetScenes())
            {
                sceneRecords.Add(new EditorSceneRecord()
                {
                    path = scene.path,
                    wasActive = scene.path == activeScene.path,
                    wasDirty = scene.isDirty,
                    wasLoaded = scene.isLoaded,
                    LastCleanTime = DateTimeOffset.Now
                });
            }
            UpdateSceneRepository();

            EditorSceneManager.activeSceneChangedInEditMode += ActiveSceneChanged;
            EditorSceneManager.sceneOpened += SceneOpened;
            EditorSceneManager.sceneClosed += SceneClosed;
            EditorSceneManager.sceneDirtied += SceneDirtied;
            EditorSceneManager.sceneSaved += SceneSaved;
            EditorSceneManager.newSceneCreated += SceneCreated;
            Undo.undoRedoPerformed += UndoRedo;
            EditorApplication.update += EditorUpdate;
        }

        public void Dispose()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= ActiveSceneChanged;
            EditorSceneManager.sceneOpened -= SceneOpened;
            EditorSceneManager.sceneClosed -= SceneClosed;
            EditorSceneManager.sceneDirtied -= SceneDirtied;
            EditorSceneManager.sceneSaved -= SceneSaved;
            EditorSceneManager.newSceneCreated -= SceneCreated;
            Undo.undoRedoPerformed -= UndoRedo;
            EditorApplication.update -= EditorUpdate;
        }

        public void Quit()
        {
            //clean quits means we dont need to keep around this state to recover
            sceneRecords.Clear();
            UpdateSceneRepository();
        }

        public static string GetAutoSavePath(string scenePath)
        {
            if (scenePath.Length == 0) return "Assets/.SceneAutoSave/Untitled";
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            string result = string.Concat(
                //a slight preference for unity-style paths means no Path class
                scenePath.Substring(0, scenePath.Length - sceneName.Length - ".unity".Length),
                //we use a hidden folder so unity wont do an import each time we create an autosave
                ".SceneAutoSave/",
                sceneName);
            return result;
        }

        private void SceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            autoSaveScenes.Add(scene.path, new AutoSaveScene(scene));
            sceneRecords.Add(new EditorSceneRecord
            {
                wasLoaded = mode != NewSceneMode.Additive,
                path = scene.path, //will be empty, incidentally
                LastCleanTime = DateTimeOffset.Now
            });
            UpdateSceneRepository();
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
            sceneRecords.Add(new EditorSceneRecord
            {
                wasLoaded = mode != OpenSceneMode.AdditiveWithoutLoading,
                path = scene.path,
                LastCleanTime = DateTimeOffset.Now
            });
            UpdateSceneRepository();
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
                targetScenePath = autoSaveScenes.Keys.SingleOrDefault(p => !openedScenes.Contains(p));
            }
            else
            {
                targetScenePath = scene.path;
            }

            _ = autoSaveScenes.Remove(targetScenePath);
            _ = sceneRecords.RemoveAll(sr => sr.path == targetScenePath);

            UpdateSceneRepository();
        }

        private void SceneSaved(Scene scene)
        {
            EditorSceneRecord newSceneRecord = sceneRecords.SingleOrDefault(sr => sr.path.Length == 0);
            if (newSceneRecord != null && scene.path != null)
            {
                newSceneRecord.path = scene.path;
                newSceneRecord.SetDirtiness(scene.isDirty);

                autoSaveScenes[scene.path] = autoSaveScenes[string.Empty];
                _ = autoSaveScenes.Remove(string.Empty);
            }
            else
            {
                sceneRecords.Single(sr => sr.path == scene.path).SetDirtiness(scene.isDirty);
            }

            UpdateSceneRepository();
        }

        private void SceneDirtied(Scene scene)
        {
            sceneRecords.Single(sr => sr.path == scene.path).SetDirtiness(scene.isDirty);
            UpdateSceneRepository();
        }

        private void ActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (string.IsNullOrEmpty(newScene.path)) return;
            foreach (EditorSceneRecord sr in sceneRecords)
            {
                sr.wasActive = newScene.path == sr.path;
            }
            UpdateSceneRepository();
        }

        //there are no scene-related events for when a scene is undid back into a clean state
        private void UndoRedo()
        {
            foreach (AutoSaveScene scene in autoSaveScenes.Values)
            {
                sceneRecords.Single(sr => sr.path == scene.Scene.path).SetDirtiness(scene.Scene.isDirty);
            }
            UpdateSceneRepository();
        }

        //this is a workaround for what is likely a unity 2019 bug
        //when unit tests are over, the previously open scenes are put back in place, but no event is triggered
        //this is likely why future unity versions have sceneManagerSetupRestored
        private void EditorUpdate()
        {
            //after a scene is closed, there should be no time for this to trigger before the replacement is opened
            if (autoSaveScenes.Count > 0) return;

            Scene activeScene = SceneManager.GetActiveScene();
            foreach (Scene scene in GetScenes())
            {
                autoSaveScenes.Add(scene.path, new AutoSaveScene(scene));
                sceneRecords.Add(new EditorSceneRecord()
                {
                    path = scene.path,
                    wasActive = scene.path == activeScene.path,
                    wasDirty = scene.isDirty,
                    wasLoaded = scene.isLoaded,
                    LastCleanTime = DateTimeOffset.Now
                });
            }

            UpdateSceneRepository();
        }

        //which is to say both that an improper close happened, and there is something to recover to
        private static bool IsRecoveryNeeded(EditorSceneRecord sceneRecord)
        {
            Scene scene = SceneManager.GetSceneByPath(sceneRecord.path);

            //this happens on domain reload, where scenes remain dirty
            //so nothing has been lost, and nothing needs to be recovered
            if (scene.IsValid() && scene.isDirty) return false;

            //could also go modified time
            //but since these are never intended to be modified, creation time is the best match
            DirectoryInfo autoSavePath = new DirectoryInfo(GetAutoSavePath(sceneRecord.path));
            if (!autoSavePath.Exists) return false;

            FileInfo latestAutoSave = autoSavePath
                .GetFiles("*.unity")
                .OrderByDescending(f => f.CreationTimeUtc)
                .FirstOrDefault();
            if (latestAutoSave == null)
            {
                return false;
            }

            bool sceneNoLongerOpened = !scene.IsValid();
            bool dirtySceneNoLongerDirty = sceneRecord.wasDirty && !scene.isDirty;
            bool autoSaveNewerThanLastCleanTime = latestAutoSave.CreationTimeUtc > sceneRecord.LastCleanTime;

            bool isRecoveryNeeded = sceneNoLongerOpened || dirtySceneNoLongerDirty || autoSaveNewerThanLastCleanTime;
            return isRecoveryNeeded;
        }

        private void UpdateSceneRepository() => sceneStateRepository.UpdateScenes(sceneRecords);

        private static IEnumerable<Scene> GetScenes() => Enumerable
            .Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i));
    }
}
