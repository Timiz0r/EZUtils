namespace EZUtils.EditorEnhancements.Tests
{
    public class AutoSavedSceneRecord
    {
        public AutoSavedSceneRecord(string originalPath, string autoSavePath)
        {
            OriginalPath = originalPath;
            AutoSavePath = autoSavePath;
        }

        public string OriginalPath { get; }
        public string AutoSavePath { get; }
    }
}
