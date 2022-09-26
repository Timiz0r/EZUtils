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

    //TODO: we don't get defaulty strings when tables don't exist
    //so, always generate a default table with no entries, allowing us to get them
    //aka via default table reference. not sure how it works for assets; probably not well, but that's fine.
    //TODO: the default values of generated entries should be the key names themselves, or maybe we provide our own
    //or maybe just for the default locale
    //TODO: look into better utilizing starup locale thingies. we still need some way to allow picking the locale,
    //and this seems like a good generic way to allow it, versus our own convention.
    public class EZLocalization
    {
        private static readonly Dictionary<string, EZLocalization> initializedLocalizations = new Dictionary<string, EZLocalization>();

        private readonly LocalizationSettings localizationSettings;
        private readonly string path;
        private readonly LocaleIdentifier[] supportedLocales;

        public static IReadOnlyList<EZLocalization> InitializedLocalizations => initializedLocalizations.Values.ToArray();
        public bool IsInEditMode => LocalizationEditorSettings.ActiveLocalizationSettings == localizationSettings;

        public string Name => localizationSettings.name;

        private EZLocalization(
            LocalizationSettings localizationSettings, string path, LocaleIdentifier[] supportedLocales)
        {
            this.localizationSettings = localizationSettings;
            this.path = path;
            this.supportedLocales = supportedLocales;
        }

        public static EZLocalization Create(string directory, params LocaleIdentifier[] supportedLocales)
        {
            //we have this cache because it's very supported to have multiple instantiations of the same instance
            if (initializedLocalizations.TryGetValue(directory, out EZLocalization existing))
            {
                //TODO: would be good to ensure locales are in-sync
                return existing;
            }

            LocalizationSettings localizationSettings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(directory);
            if (localizationSettings == null)
            {
                localizationSettings = ScriptableObject.CreateInstance<LocalizationSettings>();
                //so the last path component of the directory
                localizationSettings.name = Path.GetFileNameWithoutExtension(directory);
                localizationSettings.GetMetadata().AddMetadata(new Marker());
                _ = Directory.CreateDirectory(directory);
                AssetDatabase.CreateAsset(
                    localizationSettings, Path.Combine(directory, $"{localizationSettings.name}.asset"));
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
                ?? locales.Locales.FirstOrDefault();
            localizationSettings.SetSelectedLocale(localeToSelect);

            EZLocalization result = new EZLocalization(localizationSettings, directory, supportedLocales);
            initializedLocalizations.Add(directory, result);
            return result;
        }

        public void EnterEditMode()
        {
            if (IsInEditMode) return;
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null)
            {
                MetadataCollection metadata = LocalizationEditorSettings.ActiveLocalizationSettings.GetMetadata();
                if (metadata.HasMetadata<Marker>() && InitializedLocalizations.Count != 0) throw new InvalidOperationException(
                    $"'{LocalizationEditorSettings.ActiveLocalizationSettings.name}' is already being edited.");
            }

            LocalizationEditorSettings.ActiveLocalizationSettings = localizationSettings;

            //TODO: double check this is set. might not be upon first creation of localizationsettings
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
                while (currentCultureInfo != null && currentCultureInfo == CultureInfo.InvariantCulture)
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

            localizationSettings.ResetState();
            _ = localizationSettings.GetInitializationOperation().WaitForCompletion();
        }

        public static void StopEditing()
        {
            LocalizationEditorSettings.ActiveLocalizationSettings = null;
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
            }//localizationSettings.GetStringDatabase().GetLocalizedString("a");
            LocalizedStringDatabase stringDatabase = localizationSettings.GetStringDatabase();

            LocalizedAssetDatabase assetDatabase = localizationSettings.GetAssetDatabase();

            LocalizationContext context = new LocalizationContext(
                this,
                keyPrefix,
                stringDatabase, stringTableName,
                assetDatabase, assetTableName);
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

                localizationSettings.ResetState();
                _ = localizationSettings.GetInitializationOperation().WaitForCompletion();

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

        [Metadata(AllowedTypes = MetadataType.LocalizationSettings)]
        [Serializable]
        public class Marker : IMetadata
        {
        }
    }
}
