namespace EZUtils.EditorEnhancements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;

    public interface ISceneStateRepository
    {
        void UpdateScenes(IEnumerable<EditorSceneRecord> sceneRecords);
        IReadOnlyList<EditorSceneRecord> RecoverScenes();
    }
    public class SceneStateRepository : ISceneStateRepository
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

        [Serializable]
        private class EditorRecord
        {
            public List<EditorSceneRecord> scenes = new List<EditorSceneRecord>();
        }
    }
}
