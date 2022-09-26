namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.Localization;
    using UnityEditor.Localization.Addressables;
    using UnityEngine;
    using UnityEngine.Localization;
    using UnityEngine.Localization.Metadata;
    using UnityEngine.Localization.Settings;
    using UnityEngine.Localization.Tables;

    public class EZLocalization
    {
        private readonly LocalizationSettings localizationSettings;
        private readonly string path;
        private readonly LocaleIdentifier[] supportedLocales;

        public bool IsInEditMode => LocalizationSettings.Instance == localizationSettings;

        private EZLocalization(
            LocalizationSettings localizationSettings, string path, LocaleIdentifier[] supportedLocales)
        {
            this.localizationSettings = localizationSettings;
            this.path = path;
            this.supportedLocales = supportedLocales;
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

            SystemLocaleSelector localeSelector = new SystemLocaleSelector();
            ILocalesProvider locales = localizationSettings.GetAvailableLocales();
            Locale localeToSelect = localeSelector.GetStartupLocale(locales)
                ?? locales.Locales.FirstOrDefault(
                    l => l.Identifier.CultureInfo.TwoLetterISOLanguageName.Equals(
                        "en", StringComparison.OrdinalIgnoreCase))
                ?? locales.Locales.First();
            localizationSettings.SetSelectedLocale(localeToSelect);

            return new EZLocalization(localizationSettings, path, supportedLocales);
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

            List<Locale> addedLocales = new List<Locale>();
            foreach (LocaleIdentifier localeIdentifier in supportedLocales)
            {
                if (LocalizationEditorSettings.GetLocale(localeIdentifier) != null) return;

                Locale locale = ScriptableObject.CreateInstance<Locale>();
                locale.Identifier = localeIdentifier;
                locale.name = localeIdentifier.CultureInfo.EnglishName;
                string localePath = Path.Combine(path, $"{localeIdentifier.CultureInfo.EnglishName}.asset");
                AssetDatabase.CreateAsset(locale, localePath);
                LocalizationEditorSettings.AddLocale(locale);
                addedLocales.Add(locale);
            }

            if (addedLocales.Count == 0) return;

            //logic more or less referenced from LocaleGeneratorWindow
            IReadOnlyCollection<Locale> allLocales = LocalizationEditorSettings.GetLocales();
            Dictionary<string, Locale> localeMap =
                allLocales.ToDictionary(l => l.Identifier.Code, l => l);
            foreach (Locale locale in allLocales)
            {
                CultureInfo currentCultureInfo = locale.Identifier.CultureInfo?.Parent;
                while (currentCultureInfo != null)
                {
                    if (localeMap.TryGetValue(currentCultureInfo.Name, out Locale foundParent))
                    {
                        locale.Metadata.AddMetadata(new FallbackLocale(foundParent));
                        EditorUtility.SetDirty(locale);
                        break;
                    }

                    currentCultureInfo = currentCultureInfo.Parent;
                }
            }
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
    }
}
