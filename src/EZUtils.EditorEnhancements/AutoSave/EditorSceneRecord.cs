namespace EZUtils.EditorEnhancements
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
        public DateTimeOffset lastCleanTime
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
                lastCleanTime = DateTime.UtcNow;
            }
        }
    }
}
