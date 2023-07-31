namespace EZUtils.EditorEnhancements.AutoSave
{
    using System;
    using UnityEditor;

#pragma warning disable CA1001 //Type owns disposable field(s) but is not disposable; by design not a problem
    public class AutoSaver
#pragma warning restore CA1001
    {
        internal static readonly TimeSpanEditorPreference Interval =
            new TimeSpanEditorPreference("EZUtils.EditorEnhancements.AutoSave.Interval", TimeSpan.FromMinutes(1));
        internal static readonly EditorPreference<bool> Enabled =
            new EditorPreference<bool>("EZUtils.EditorEnhancements.AutoSave.Enabled", true);

        private DateTimeOffset lastAutoSaveTime = DateTimeOffset.Now;
        private readonly SceneAutoSaver sceneAutoSaver = new SceneAutoSaver(new SceneRecoveryRepository());

        public static void Enable() => Enabled.Value = true;
        public static void Disable() => Enabled.Value = false;

        //grabbing the on-load set of scenes doesnt work well without a delay call
        //also, on first start, only the splash screen is showing,
        //and it's somewhat stylistically preferred to have the full editor showing
        [InitializeOnLoadMethod]
        private static void UnityInitialize() => EditorApplication.delayCall += () =>
        {
            AutoSaver autoSave = new AutoSaver();

            autoSave.sceneAutoSaver.Load();

            EditorApplication.update += autoSave.EditorUpdate;
            EditorApplication.quitting += autoSave.EditorQuitting;
        };

        private void EditorUpdate()
        {
            if (!Enabled.Value) return;
            if (DateTimeOffset.Now - lastAutoSaveTime <= Interval.Value) return;

            sceneAutoSaver.AutoSave();

            lastAutoSaveTime = DateTimeOffset.Now;
        }

        private void EditorQuitting() => sceneAutoSaver.Quit();
    }
}
