namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.Localization;
    using UnityEditor.Localization.Addressables;
    using UnityEngine;
    using UnityEngine.Localization;
    using UnityEngine.Localization.Settings;
    using UnityEngine.Localization.Tables;
    using Object = UnityEngine.Object;

    public class EZLocalization
    {
        private readonly LocalizationSettings localizationSettings;
        private readonly string path;

        public bool IsInEditMode => LocalizationSettings.Instance == localizationSettings;

        private EZLocalization(LocalizationSettings localizationSettings, string path)
        {
            this.localizationSettings = localizationSettings;
            this.path = path;
        }

        public static EZLocalization Create(string path, params LocaleIdentifier[] supportedLocales)
        {
            LocalizationSettings localizationSettings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(path);
            if (localizationSettings == null)
            {
                localizationSettings = ScriptableObject.CreateInstance<LocalizationSettings>();
                localizationSettings.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(localizationSettings, path);
            }
            //will see if we can get away with not using addressibles, since we generate don't need it for editor applications
            //not that we cant use them, but I suspect it would pollute other projects in undesirable ways
            _ = localizationSettings.GetInitializationOperation().WaitForCompletion();

            ILocalesProvider locales = LocalesProvider.Create(path, localizationSettings, supportedLocales);
            //even though we only ever use one local for lookups, we want all locales in so that we can
            //add translations in editor (assuming editor supports what we're doing here without addressibles)
            localizationSettings.SetAvailableLocales(locales);


            SystemLocaleSelector localeSelector = new SystemLocaleSelector();
            Locale localeToSelect = localeSelector.GetStartupLocale(locales)
                ?? locales.Locales.FirstOrDefault(
                    l => l.Identifier.CultureInfo.TwoLetterISOLanguageName.Equals(
                        "en", StringComparison.OrdinalIgnoreCase))
                ?? locales.Locales.First();
            localizationSettings.SetSelectedLocale(localeToSelect);

            return new EZLocalization(localizationSettings, path);
        }

        public void EnterEditMode()
        {
            if (IsInEditMode) return;
            if (LocalizationSettings.Instance != null) throw new InvalidOperationException(
                $"'{LocalizationSettings.Instance.name}' is already being edited.");

            LocalizationSettings.Instance = localizationSettings;

            string pattern = $"EZLocalization-{localizationSettings.name}";
            AddressableGroupRules addressableGroupRules = ScriptableObject.CreateInstance<AddressableGroupRules>();
            addressableGroupRules.LocaleResolver = new GroupResolver(pattern, pattern);
            addressableGroupRules.AssetResolver = new GroupResolver(pattern, pattern);
            addressableGroupRules.StringTablesResolver = new GroupResolver(pattern, pattern);
            addressableGroupRules.AssetTablesResolver = new GroupResolver(pattern, pattern);
            AddressableGroupRules.Instance = addressableGroupRules;
        }

        public static void StopEditing()
        {
            LocalizationSettings.Instance = null;
            AddressableGroupRules.Instance = null;
        }

        public LocalizationContext GetContext(string group, string keyPrefix)
        {
            string groupDirectory = Path.Combine(path, group);
            _ = Directory.CreateDirectory(groupDirectory);
            string stringTableName = $"{group}-Strings";
            string assetTableName = $"{group}-Assets";

            if (IsInEditMode)
            {
                EnsureGroupExists();
            }

            StringTable stringTable = localizationSettings.GetStringDatabase().GetTable(stringTableName);
            if (stringTable == null) throw new ArgumentOutOfRangeException(
                nameof(group), $"Group '{group}' does not exist.");

            AssetTable assetTable = localizationSettings.GetAssetDatabase().GetTable(assetTableName);
            if (assetTable == null) throw new ArgumentOutOfRangeException(
                nameof(group), $"Group '{group}' does not exist.");

            LocalizationContext context = new LocalizationContext(this, stringTable, assetTable);
            return context;

            void EnsureGroupExists()
            {
                StringTableCollection stringTableCollection = LocalizationEditorSettings.GetStringTableCollection(stringTableName);
                if (stringTableCollection == null)
                {
                    _ = LocalizationEditorSettings.CreateStringTableCollection(
                        stringTableName,
                        groupDirectory,
                        localizationSettings.GetAvailableLocales().Locales);
                }
                else
                {
                    EnsureTablesExist(stringTableCollection);
                }

                AssetTableCollection assetTableCollection = LocalizationEditorSettings.GetAssetTableCollection(assetTableName);
                if (assetTableCollection == null)
                {
                    _ = LocalizationEditorSettings.CreateAssetTableCollection(
                        assetTableName,
                        groupDirectory,
                        localizationSettings.GetAvailableLocales().Locales);
                }
                else
                {
                    EnsureTablesExist(assetTableCollection);
                }

                void EnsureTablesExist(LocalizationTableCollection tableCollection)
                {
                    foreach (Locale locale in localizationSettings.GetAvailableLocales().Locales)
                    {
                        if (tableCollection.ContainsTable(locale.Identifier)) return;
                        _ = tableCollection.AddNewTable(locale.Identifier);
                    }
                }
            }
        }

        //TODO: use the normal one instead
        private class LocalesProvider : ILocalesProvider
        {
            private readonly List<Locale> locales;

            private LocalesProvider(List<Locale> locales)
            {
                this.locales = locales;
            }

            public static LocalesProvider Create(string path, Object parentAsset, params LocaleIdentifier[] supportedLocales)
            {
                List<Locale> locales = new List<Locale>(supportedLocales?.Length ?? 0);
                Locale[] existingLocales = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Locale>().ToArray();
                foreach (LocaleIdentifier localeIdentifier in supportedLocales ?? Enumerable.Empty<LocaleIdentifier>())
                {
                    Locale locale = existingLocales.SingleOrDefault(l => l.Identifier == localeIdentifier);
                    if (locale == null)
                    {
                        locale = Locale.CreateLocale(localeIdentifier);
                        AssetDatabase.AddObjectToAsset(parentAsset, locale);
                    }
                }

                LocalesProvider result = new LocalesProvider(locales);
                return result;
            }

            List<Locale> ILocalesProvider.Locales => locales;

            //it's not clear how these two get called, so want to avoid implementing them to see how
            Locale ILocalesProvider.GetLocale(LocaleIdentifier id) => throw new NotImplementedException();
            void ILocalesProvider.AddLocale(Locale locale) => throw new NotImplementedException();
            bool ILocalesProvider.RemoveLocale(Locale locale) => throw new NotImplementedException();
        }
    }
}
