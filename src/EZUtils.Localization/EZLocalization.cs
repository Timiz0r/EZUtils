namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Localization;
    using UnityEngine.Localization.Settings;
    using Object = UnityEngine.Object;

    public class EZLocalization
    {
        private readonly LocalizationSettings localizationSettings;

        private EZLocalization(LocalizationSettings localizationSettings)
        {
            this.localizationSettings = localizationSettings;
        }

        public static EZLocalization Create(string path, params LocaleIdentifier[] supportedLocales)
        {
            throw new NotImplementedException();
            LocalizationSettings localizationSettings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(path);
            if (localizationSettings == null)
            {
                localizationSettings = ScriptableObject.CreateInstance<LocalizationSettings>();
                localizationSettings.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(localizationSettings, path);
            }
            //will see if we can get away with not using addressibles, since we generate don't need it for editor applications
            //not that we cant use them, but I suspect it would pollute other projects in undesirable ways
            // localizationSettings.GetInitializationOperation().WaitForCompletion();

            ILocalesProvider locales = LocalesProvider.Create(path, localizationSettings, supportedLocales);
            //even though we only ever use one local for lookups, we want all locales in so that we can
            //add translations in editor (assuming editor supports what we're doing here without addressibles)
            localizationSettings.SetAvailableLocales(locales);

            // localizationSettings.SetStringDatabase()

            SystemLocaleSelector localeSelector = new SystemLocaleSelector();
            Locale localeToSelect = localeSelector.GetStartupLocale(locales)
                ?? locales.Locales.FirstOrDefault(
                    l => l.Identifier.CultureInfo.TwoLetterISOLanguageName.Equals(
                        "en", StringComparison.OrdinalIgnoreCase))
                ?? locales.Locales.First();
            localizationSettings.SetSelectedLocale(localeToSelect);
        }


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
