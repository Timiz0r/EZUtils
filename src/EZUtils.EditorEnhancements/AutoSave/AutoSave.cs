namespace EZUtils.EditorEnhancements
{
    using System;
    using UnityEditor;

    internal class AutoSave
    {
        internal static readonly TimeSpanEditorPreference Interval =
            new TimeSpanEditorPreference("EZUtils.EditorEnhancements.AutoSave.Interval", TimeSpan.FromMinutes(1));
        internal static readonly EditorPreference<bool> Enabled =
            new EditorPreference<bool>("EZUtils.EditorEnhancements.AutoSave.Interval", true);

        private DateTimeOffset lastAutoSaveTime = DateTimeOffset.Now;
        private readonly SceneAutoSaver sceneAutoSaver = new SceneAutoSaver();

        [InitializeOnLoadMethod]
        private static void UnityInitialize()
        {
            AutoSave autoSave = new AutoSave();

            autoSave.sceneAutoSaver.Load();

            EditorApplication.update += autoSave.EditorUpdate;
        }

        private void EditorUpdate()
        {
            if (!Enabled.Value) return;
            if (DateTimeOffset.Now - lastAutoSaveTime <= Interval.Value) return;

            sceneAutoSaver.AutoSave();

            lastAutoSaveTime = DateTimeOffset.Now;
        }
    }
}
