namespace EZUtils.EditorEnhancements
{
    using System;
    using System.IO;
    using System.Linq;
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;

    internal class AutoSaveScene
    {
        private readonly DirectoryInfo autoSaveFolder;
        private readonly string sceneName;
        public string Path { get; }

        public AutoSaveScene(string scenePath)
        {
            Path = scenePath;
            sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            autoSaveFolder = new DirectoryInfo(string.Concat(
                //a slight preference for unity-style paths means no Path class
                Path.Substring(0, Path.Length - sceneName.Length - ".unity".Length),
                //we use a hidden folder so unity wont do an import each time we create an autosave
                ".SceneAutoSave/",
                sceneName));
        }

        public bool IsAutoSaveNewer()
        {
            if (!autoSaveFolder.Exists) return false;

            FileInfo sceneFile = new FileInfo(Path);
            //could also go modified time
            //but since these are never intended to be modified, creation time is the best match
            FileInfo latestAutoSave = autoSaveFolder
                .GetFiles()
                .OrderByDescending(f => f.CreationTimeUtc)
                .FirstOrDefault();

            bool result = latestAutoSave != null && latestAutoSave.CreationTimeUtc > sceneFile.LastWriteTimeUtc;
            return result;
        }

        public void AutoSave()
        {
            if (!autoSaveFolder.Exists) autoSaveFolder.Create();

            FileInfo sceneFile = new FileInfo(Path);
            _ = sceneFile.CopyTo(
                System.IO.Path.Combine(
                    autoSaveFolder.FullName,
                    $"{sceneName}-AutoSave-{DateTimeOffset.Now:yyyy'-'MM'-'dd'T'HHmmss}.unity"));

            FileInfo[] autoSaveFiles = autoSaveFolder.GetFiles();
            if (autoSaveFiles.Length > SceneAutoSaver.SceneAutoSaveCopies.Value)
            {
                autoSaveFiles.OrderBy(f => f.CreationTimeUtc).First().Delete();
            }
        }
        public void Recover()
        {
            Scene actualScene = Enumerable
                .Range(0, SceneManager.sceneCount)
                .Select(i => SceneManager.GetSceneAt(i))
                .SingleOrDefault(s => s.path == Path);
            if (actualScene == default) actualScene = EditorSceneManager.OpenScene(Path, OpenSceneMode.Additive);

            FileInfo latestAutoSaveFile = !autoSaveFolder.Exists
                ? null
                : autoSaveFolder
                    .GetFiles()
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .FirstOrDefault();
            //so we recovered towards the set of open scenes, but we can't load an autosave
            if (latestAutoSaveFile == null) return;

            //TODO: if this doesn't work well, then will need to mess with loading
            //and if it does work well, we don't need to have a scene var. so try opening without checking anything
            _ = latestAutoSaveFile.CopyTo(Path);
        }
    }
}
