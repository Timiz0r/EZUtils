namespace EZUtils.EditorEnhancements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;

    using static Localization;

    public interface ISceneRecoveryRepository
    {
        void UpdateScenes(IEnumerable<EditorSceneRecord> sceneRecords);
        IReadOnlyList<EditorSceneRecord> RecoverScenes();
        bool MayPerformRecovery();
    }
    public class SceneRecoveryRepository : ISceneRecoveryRepository
    {
        private readonly EditorPreference<string> rawEditorRecord = new EditorPreference<string>(
            "EZUtils.EditorEnhancements.AutoSave.Scene.EditorRecord", null);

        public void UpdateScenes(IEnumerable<EditorSceneRecord> sceneRecords)
            => rawEditorRecord.Value = EditorJsonUtility.ToJson(new EditorRecord() { scenes = sceneRecords.ToList() });

        public IReadOnlyList<EditorSceneRecord> RecoverScenes()
        {
            EditorRecord previousEditorRecord = new EditorRecord();
            if (rawEditorRecord.Value is string r)
            {
                EditorJsonUtility.FromJsonOverwrite(r, previousEditorRecord);
            }

            return previousEditorRecord.scenes;
        }

        public bool MayPerformRecovery() => EditorUtility.DisplayDialog(
            T("Scene auto-save"),
            T("It does not appear that Unity was properly closed, but there is auto-save data available. " +
                "Attempt to recover using auto-save data?"),
            T("Recover"),
            T("Do not recover"));

        [Serializable]
        private class EditorRecord
        {
            public List<EditorSceneRecord> scenes = new List<EditorSceneRecord>();
        }
    }
}
