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

        //grabbing the on-load set of scenes doesnt work well without a delay call
        //also, on first start, only the splash screen is showing,
        //and it's somewhat stylistically preferred to have the full editor showing
        [InitializeOnLoadMethod]
        private static void UnityInitialize() => EditorApplication.delayCall += () =>
        {
            AutoSave autoSave = new AutoSave();

            autoSave.sceneAutoSaver.Load();

            EditorApplication.update += autoSave.EditorUpdate;
        };

        private void EditorUpdate()
        {
            if (!Enabled.Value) return;
            if (DateTimeOffset.Now - lastAutoSaveTime <= Interval.Value) return;

            sceneAutoSaver.AutoSave();

            lastAutoSaveTime = DateTimeOffset.Now;
        }
    }
}
