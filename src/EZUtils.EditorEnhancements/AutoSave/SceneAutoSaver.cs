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

    public class SceneAutoSaver : IDisposable
    {
        private readonly Dictionary<string, AutoSaveScene> autoSaveScenes = new Dictionary<string, AutoSaveScene>();
        private readonly List<EditorSceneRecord> sceneRecords = new List<EditorSceneRecord>();
        private readonly ISceneRecoveryRepository sceneRecoveryRepository;

        public SceneAutoSaver(ISceneRecoveryRepository sceneRecoveryRepository)
        {
            this.sceneRecoveryRepository = sceneRecoveryRepository;
        }

        public void AutoSave()
        {
            foreach (AutoSaveScene scene in autoSaveScenes.Values)
            {
                if (!scene.Scene.isDirty) return;
                scene.AutoSave();
            }
        }

        public void Load()
        {
            IReadOnlyList<EditorSceneRecord> recoveredScenes = sceneRecoveryRepository.RecoverScenes();

            if (recoveredScenes.Any(sr => IsRecoveryNeeded(sr)) && sceneRecoveryRepository.MayPerformRecovery())
            {
                foreach (EditorSceneRecord sceneRecord in recoveredScenes)
                {
                    Scene scene = SceneManager.GetSceneByPath(sceneRecord.path);
                    if (!scene.IsValid())
                    {
                        if (sceneRecord.path.Length == 0)
                        {
                            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                        }
                        else if (File.Exists(sceneRecord.path))
                        {
                            scene = EditorSceneManager.OpenScene(sceneRecord.path, OpenSceneMode.Additive);
                        }
                        else
                        {
                            //when unity crashed, the scene asset was deleted,
                            //which leaves a dirty scene with existing path up
                            //the only way to get back to this state is to save a new scene, then delete it
                            scene = EditorSceneManager.NewScene(
                                NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                            _ = EditorSceneManager.SaveScene(scene, sceneRecord.path);
                            _ = AssetDatabase.DeleteAsset(sceneRecord.path);

                        }
                    }

                    AutoSaveScene autoSaveScene = new AutoSaveScene(scene, sceneRecoveryRepository);
                    autoSaveScenes.Add(sceneRecord.path, autoSaveScene);

                    if (sceneRecord.wasActive)
                    {
                        _ = SceneManager.SetActiveScene(scene);
                    }

                    if (sceneRecord.wasLoaded)
                    {
                        //unloading a scene requires saving or discarding as decided by user
                        //so if a scene is unloaded, there's ultimately nothing to recover
                        autoSaveScene.Recover(sceneRecord.LastCleanTime);
                    }
                    else
                    {
                        _ = EditorSceneManager.CloseScene(scene, removeScene: false);
                    }
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
                    autoSaveScenes.Add(scene.path, new AutoSaveScene(scene, sceneRecoveryRepository));
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
            EditorSceneManager.sceneClosing += SceneClosing;
            EditorSceneManager.sceneDirtied += SceneDirtied;
            EditorSceneManager.sceneSaved += SceneSaved;
            EditorSceneManager.newSceneCreated += SceneCreated;
            Undo.undoRedoPerformed += UndoRedo;
            EditorApplication.update += EditorUpdate;
            SceneAssetPostProcessor.SceneAssetMoved += SceneAssetMoved;
            SceneHierarchyHooks.addItemsToSceneHeaderContextMenu += AddSceneHeaderContextMenuItem;
        }

        public void Dispose()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= ActiveSceneChanged;
            EditorSceneManager.sceneOpened -= SceneOpened;
            EditorSceneManager.sceneClosing -= SceneClosing;
            EditorSceneManager.sceneDirtied -= SceneDirtied;
            EditorSceneManager.sceneSaved -= SceneSaved;
            EditorSceneManager.newSceneCreated -= SceneCreated;
            Undo.undoRedoPerformed -= UndoRedo;
            EditorApplication.update -= EditorUpdate;
            SceneAssetPostProcessor.SceneAssetMoved -= SceneAssetMoved;
            SceneHierarchyHooks.addItemsToSceneHeaderContextMenu -= AddSceneHeaderContextMenuItem;
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
            AutoSaveScene autoSaveScene = new AutoSaveScene(scene, sceneRecoveryRepository);
            autoSaveScenes.Add(scene.path, autoSaveScene);

            sceneRecords.Add(new EditorSceneRecord
            {
                wasLoaded = true,
                path = scene.path, //will be empty, incidentally
                LastCleanTime = DateTimeOffset.Now,
                wasActive = scene.path == SceneManager.GetActiveScene().path
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

            EditorSceneRecord existingRecord = sceneRecords.SingleOrDefault(sr => sr.path == scene.path);
            if (existingRecord != null)
            {
                //the opened event is what's triggered when an unloaded scene is loaded
                existingRecord.wasLoaded = scene.isLoaded;
            }
            else
            {
                autoSaveScenes.Add(scene.path, new AutoSaveScene(scene, sceneRecoveryRepository));
                sceneRecords.Add(new EditorSceneRecord
                {
                    wasLoaded = mode != OpenSceneMode.AdditiveWithoutLoading,
                    path = scene.path,
                    LastCleanTime = DateTimeOffset.Now
                });
            }

            UpdateSceneRepository();
        }

        //scene rename notes:
        //GetScenes has two copies of the new name and none of the old name
        //except if there are multiple scenes open, then there's only one of the new name
        //scene is for the new name
        private void SceneClosing(Scene scene, bool removingScene)
        {
            string targetScenePath;
            if (!removingScene)
            {
                EditorSceneRecord sceneRecord = sceneRecords.Single(sr => sr.path == scene.path);
                sceneRecord.wasLoaded = false;
                sceneRecord.SetDirtiness(false);
                UpdateSceneRepository();
                return;
            }

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
            if (newSceneRecord != null && scene.path.Length > 0)
            {
                newSceneRecord.path = scene.path;
                //for SceneSaved, we look at scene.isDirty because a save-as won't change the dirtiness
                //so we can't simply assume saving always means a scene becoming clean
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
                autoSaveScenes.Add(scene.path, new AutoSaveScene(scene, sceneRecoveryRepository));
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

        private void SceneAssetMoved(string fromPath, string toPath)
        {
            if (!autoSaveScenes.TryGetValue(fromPath, out AutoSaveScene fromScene)) return;

            autoSaveScenes.Add(toPath, fromScene);
            _ = autoSaveScenes.Remove(fromPath);

            sceneRecords.Single(sr => sr.path == fromPath).path = toPath;
        }

        private void AddSceneHeaderContextMenuItem(GenericMenu menu, Scene scene)
        {
            EditorSceneRecord sceneRecord = sceneRecords.Single(sr => sr.path == scene.path);
            FileInfo latestAutoSave = GetLatestAutoSave(sceneRecord);
            AutoSaveScene autoSaveScene = autoSaveScenes[scene.path];

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(T("Create manual auto-save")), on: false, () => autoSaveScene.AutoSave());

            GUIContent revertLatestContent = new GUIContent(T("Revert to latest auto-save"));
            if (latestAutoSave != null && sceneRecord.LastCleanTime < latestAutoSave.CreationTimeUtc)
            {
                menu.AddItem(revertLatestContent, on: false, () => autoSaveScene.Recover(sceneRecord.LastCleanTime));
            }
            else
            {
                menu.AddDisabledItem(revertLatestContent);
            }

            foreach (FileInfo autoSave in GetAvailableAutoSaves(sceneRecord).OrderByDescending(f => f.CreationTimeUtc))
            {
                //NOTE: we use F formatter because, afaik, unity doesnt support escaping the path separator
                //and even if it did, we have no way of doing it at the moment,
                //since the EZLocalization locale isn't currently accessible outside of the T call
                //still, there are perhaps locales where F still produces path separators, so this isn't an ideal fix
                GUIContent revertContent = new GUIContent(T($"Revert to auto-save/{autoSave.CreationTime:F}"));
                menu.AddItem(revertContent, on: false, () => autoSaveScene.ForceRecoverSpecific(autoSave));
            }
        }

        //which is to say both that an improper close happened, and there is something to recover to
        private static bool IsRecoveryNeeded(EditorSceneRecord sceneRecord)
        {
            Scene scene = SceneManager.GetSceneByPath(sceneRecord.path);

            //this happens on domain reload, where scenes remain dirty
            //so nothing has been lost, and nothing needs to be recovered
            if (scene.IsValid() && scene.isDirty) return false;

            FileInfo latestAutoSave = GetLatestAutoSave(sceneRecord);
            if (latestAutoSave == null)
            {
                return false;
            }

            bool sceneNoLongerOpened = !scene.IsValid();
            bool dirtySceneNoLongerDirty = sceneRecord.wasDirty && !scene.isDirty;
            bool autoSaveNewerThanLastCleanTime = latestAutoSave.CreationTimeUtc > sceneRecord.LastCleanTime;

            bool isRecoveryNeeded = sceneNoLongerOpened
                || (!sceneRecord.wasLoaded && scene.isLoaded)
                || (dirtySceneNoLongerDirty && autoSaveNewerThanLastCleanTime);
            return isRecoveryNeeded;
        }

        private static FileInfo GetLatestAutoSave(EditorSceneRecord sceneRecord)
        {
            IReadOnlyList<FileInfo> availableAutoSaves = GetAvailableAutoSaves(sceneRecord);
            FileInfo latestAutoSave = availableAutoSaves
                //could also go modified time
                //but since these are never intended to be modified, creation time is the best match
                .OrderByDescending(f => f.CreationTimeUtc)
                .FirstOrDefault();
            return latestAutoSave;
        }

        private static IReadOnlyList<FileInfo> GetAvailableAutoSaves(EditorSceneRecord sceneRecord)
        {
            DirectoryInfo autoSavePath = new DirectoryInfo(GetAutoSavePath(sceneRecord.path));
            return !autoSavePath.Exists
                ? Array.Empty<FileInfo>()
                : autoSavePath.GetFiles("*.unity");
        }

        private void UpdateSceneRepository() => sceneRecoveryRepository.UpdateScenes(sceneRecords);

        private static IEnumerable<Scene> GetScenes() => Enumerable
            .Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i));
    }
}
