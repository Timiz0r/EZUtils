namespace EZUtils.EditorEnhancements.AutoSave
{
    using System;
    using UnityEngine;

    [Serializable]
    public class EditorSceneRecord
    {
        public string path;
        public bool wasLoaded;
        public bool wasDirty;
        public bool wasActive;

        [SerializeField]
        private long lastCleanTimeUnix;
        private DateTimeOffset? actualLastCleanTime;
        public DateTimeOffset LastCleanTime
        {
            get => actualLastCleanTime ?? (actualLastCleanTime = DateTimeOffset.FromUnixTimeMilliseconds(lastCleanTimeUnix)).Value;
            set
            {
                actualLastCleanTime = value;
                lastCleanTimeUnix = value.ToUnixTimeMilliseconds();
            }
        }

        public void SetDirtiness(bool isDirty)
        {
            if (isDirty == wasDirty) return;

            wasDirty = isDirty;
            if (!isDirty)
            {
                LastCleanTime = DateTime.UtcNow;
            }
        }
    }
}
