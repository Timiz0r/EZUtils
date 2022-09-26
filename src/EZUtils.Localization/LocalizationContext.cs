namespace EZUtils.Localization
{
    using System;
    using UnityEditor;
    using UnityEditor.Localization;
    using UnityEngine.Localization.Settings;
    using UnityEngine.Localization.Tables;

    public class LocalizationContext
    {
        private readonly EZLocalization ezLocalization;
        private readonly StringTable stringTable;
        private readonly AssetTable assetTable;
        private readonly string keyPrefix;

        public LocalizationContext(
            EZLocalization ezLocalization, StringTable stringTable, AssetTable assetTable, string keyPrefix)
        {
            this.ezLocalization = ezLocalization;
            this.stringTable = stringTable;
            this.assetTable = assetTable;
            this.keyPrefix = keyPrefix;
        }

        public LocalizationContext(EZLocalization ezLocalization, StringTable stringTable, AssetTable assetTable)
            : this(ezLocalization, stringTable, assetTable, keyPrefix: string.Empty)
        {
        }

        public LocalizationContext GetContext(string keyPrefix)
            => new LocalizationContext(ezLocalization, stringTable, assetTable, $"{this.keyPrefix}.{keyPrefix}");


        public string GetString(string key)
        {
            key = $"{keyPrefix}.{key}";
            string result = stringTable[key]?.LocalizedValue;
            if (result == null)
            {
                //so in edit mode, we'll basically provide an empty value (in addition to creating an entry)
                if (!ezLocalization.IsInEditMode) throw new ArgumentOutOfRangeException(
                    nameof(key), $"There is no value for key '{key}'.");

                _ = stringTable.SharedData.AddKey(key);
                EditorUtility.SetDirty(stringTable.SharedData);
            }

            return result;
        }

        public string GetString(string key, params object[] args)
        {
            key = $"{keyPrefix}.{key}";
            string result = stringTable[key]?.GetLocalizedString(args);
            if (result == null)
            {
                //so in edit mode, we'll basically provide an empty value (in addition to creating an entry)
                if (!ezLocalization.IsInEditMode) throw new ArgumentOutOfRangeException(
                    nameof(key), $"There is no value for key '{key}' in '{stringTable.TableCollectionName}'.");

                _ = stringTable.SharedData.AddKey(key);
                EditorUtility.SetDirty(stringTable.SharedData);
            }

            return result;
        }

        public T GetAsset<T>(string key) where T : UnityEngine.Object
        {
            key = $"{keyPrefix}.{key}";
            T result = assetTable.GetAssetAsync<T>(key).WaitForCompletion();
            if (result == null)
            {
                //so in edit mode, we'll basically provide an empty value (in addition to creating an entry)
                if (!ezLocalization.IsInEditMode) throw new ArgumentOutOfRangeException(
                    nameof(key), $"There is no value for key '{key}' in '{assetTable.TableCollectionName}'.");

                _ = assetTable.SharedData.AddKey(key);
                EditorUtility.SetDirty(assetTable.SharedData);
            }

            return result;
        }
    }
}
