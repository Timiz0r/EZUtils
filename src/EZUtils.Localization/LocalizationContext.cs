namespace EZUtils.Localization
{
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEngine.Localization.Settings;
    using UnityEngine.Localization.Tables;

    public class LocalizationContext
    {
        private readonly EZLocalization ezLocalization;
        private readonly string keyPrefix;
        private readonly LocalizedStringDatabase stringDatabase;
        private readonly string stringTableName;
        private readonly LocalizedAssetDatabase assetDatabase;
        private readonly string assetTableName;

        public LocalizationContext(
            EZLocalization ezLocalization,
            string keyPrefix,
            LocalizedStringDatabase stringDatabase,
            string stringTableName,
            LocalizedAssetDatabase assetDatabase,
            string assetTableName)
        {
            this.ezLocalization = ezLocalization;
            this.keyPrefix = keyPrefix;
            this.stringDatabase = stringDatabase;
            this.stringTableName = stringTableName;
            this.assetDatabase = assetDatabase;
            this.assetTableName = assetTableName;
        }

        public LocalizationContext GetContext(string keyPrefix)
            => new LocalizationContext(
                ezLocalization,
                keyPrefix: $"{this.keyPrefix}.{keyPrefix}",
                stringDatabase: stringDatabase,
                stringTableName: stringTableName,
                assetDatabase: assetDatabase,
                assetTableName: assetTableName);


        public string GetString(string key) => GetString(key, null);

        public string GetString(string key, params object[] args)
        {
            key = $"{keyPrefix}.{key}";

            string result = stringDatabase.GetLocalizedString(
                stringTableName, key, fallbackBehavior: FallbackBehavior.UseFallback, arguments: args);
            if (ezLocalization.IsInEditMode)
            {
                EnsureKeyExists(stringDatabase, stringTableName, key);
            }

            return result;
        }

        public T GetAsset<T>(string key) where T : UnityEngine.Object
        {
            key = $"{keyPrefix}.{key}";

            T result = assetDatabase.GetLocalizedAsset<T>(assetTableName, key);
            if (ezLocalization.IsInEditMode)
            {
                EnsureKeyExists(assetDatabase, assetTableName, key);
            }

            return result;
        }

        private static void EnsureKeyExists<TTable, TEntry>(LocalizedDatabase<TTable, TEntry> db, string tableName, string key)
            where TTable : DetailedLocalizationTable<TEntry>
            where TEntry : TableEntry
        {
            TTable table = db.GetTable(tableName);
            SharedTableData.SharedTableEntry entry = table.SharedData.Entries.SingleOrDefault(e => e.Key == key);
            if (entry != null) return;

            _ = table.SharedData.AddKey(key);
            EditorUtility.SetDirty(table.SharedData);
        }
    }
}
