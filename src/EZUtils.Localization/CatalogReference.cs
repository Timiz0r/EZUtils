namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;


    public class CatalogReference : IDisposable
    {
        private static CultureInfo newlySelectedUnityEditorLanguage;

        private readonly List<(UnityEngine.Object obj, Action action)> retranslatableObjects =
            new List<(UnityEngine.Object obj, Action action)>();
        private readonly List<(VisualElement element, Action action)> retranslatableElements =
            new List<(VisualElement element, Action action)>();
        //not sure if it's better to watch all subdirs from the project root, or to have one watcher per provided root
        //am guessing this will be better, since all of the temp folders and whatnot are pretty large
        private readonly FileSystemWatcher fsw;
        //key is full path to make the initial load and fsw paths the same, in the easiest way possible
        private readonly Dictionary<string, GetTextDocument> documents = new Dictionary<string, GetTextDocument>();
        private readonly string root;
        private readonly string selectedLocaleEditorPrefKey;
        private Locale selectedLocale;
        private GetTextCatalog catalog;
        private bool disposedValue;

        public CatalogReference(string root, Locale nativeLocale, string localeDomainSetting)
        {
            this.root = root;
            NativeLocale = selectedLocale = nativeLocale;
            selectedLocaleEditorPrefKey = $"EZUtils.Localization.SelectedLocale.{localeDomainSetting}";

            fsw = new FileSystemWatcher()
            {
                Path = root,
                Filter = "*.po",
                IncludeSubdirectories = false
            };
            fsw.Changed += PoFileChanged;
            fsw.Created += PoFileChanged;
            fsw.Deleted += PoFileChanged;
            fsw.Renamed += PoFileRenamed;
        }

        public GetTextCatalog Catalog => LazyInitializer.EnsureInitialized(ref catalog, () => InitializeCatalog());

        public Locale NativeLocale { get; }

        //NOTE: selectlocale must not use the Catalog property because the lazy initializer inside calls back into
        //SelectLocale
        public void SelectLocale(Locale locale)
        {
            selectedLocale = locale;
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, locale.CultureInfo.Name);
            catalog?.SelectLocale(locale);
            Retranslate();
        }

        public Locale SelectLocale(CultureInfo cultureInfo)
        {
            selectedLocale = catalog?.SelectLocale(cultureInfo);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, cultureInfo.Name);
            Retranslate();
            return selectedLocale;
        }

        public Locale SelectLocaleOrNative(params Locale[] locales)
        {
            selectedLocale = catalog?.SelectLocaleOrNative(locales);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, selectedLocale.CultureInfo.Name);
            Retranslate();
            return selectedLocale;
        }

        public Locale SelectLocaleOrNative(params CultureInfo[] cultureInfos)
        {
            selectedLocale = catalog?.SelectLocaleOrNative(cultureInfos);
            EditorPrefs.SetString(selectedLocaleEditorPrefKey, selectedLocale.CultureInfo.Name);
            Retranslate();
            return selectedLocale;
        }

        public void Retranslate()
        {
            _ = retranslatableObjects.RemoveAll(t => t.obj == null); //aka destroyed
            foreach ((_, Action action) in retranslatableObjects)
            {
                action();
            }

            _ = retranslatableElements.RemoveAll(t => t.element.panel == null); //aka removed from hierarchy
            foreach ((_, Action action) in retranslatableElements)
            {
                action();
            }
        }
        public void TrackRetranslatable(UnityEngine.Object obj, Action action)
        {
            retranslatableObjects.Add((obj, action));
            action();
        }
        public void TrackRetranslatable(VisualElement element, Action action)
        {
            retranslatableElements.Add((element, action));
            action();
        }

        private GetTextCatalog InitializeCatalog()
        {
            DirectoryInfo directory = new DirectoryInfo(root);
            if (directory.Exists)
            {
                foreach (FileInfo file in directory.EnumerateFiles("*.po", SearchOption.TopDirectoryOnly))
                {
                    documents.Add(file.FullName, GetTextDocument.LoadFrom(file.FullName));
                }
            }

            //technically this sets the catalog before LazyInitializer can, but that's okay
            //we reload whether the directory exists or not because empty catalogs are fine
            ReloadCatalog();
            fsw.EnableRaisingEvents = true;

            //we do this as late as possible because EZLocalization is often part of cctor of a window, and, when opening
            //the window for the first time (versus reloading unity), GetString will throw.
            string prefValue = EditorPrefs.GetString(selectedLocaleEditorPrefKey);
            if (!string.IsNullOrEmpty(prefValue))
            {
                CultureInfo locale = CultureInfo.GetCultureInfo(prefValue);
                //if newlySelectedUnityEditorLanguage is null then we'll prefer one from setting
                _ = SelectLocaleOrNative(newlySelectedUnityEditorLanguage, locale);
            }
            else if (newlySelectedUnityEditorLanguage != null)
            {
                _ = SelectLocaleOrNative(newlySelectedUnityEditorLanguage);
            }
            else
            {
                SelectLocale(NativeLocale);
            }

            return catalog;
        }

        private void ReloadCatalog()
        {
            catalog = new GetTextCatalog(documents.Values.ToArray(), NativeLocale);
            catalog.SelectLocale(selectedLocale);
            Retranslate();
        }

        //NOTE: fsw is not thread-safe in two ways:
        //1. our editing of documents
        //2. the potential retranslation of visualelements
        //so we get things back on the main thread with delaycall
        private void PoFileRenamed(object sender, RenamedEventArgs e) => EditorApplication.delayCall += () =>
        {
            if (documents.TryGetValue(e.OldFullPath, out GetTextDocument doc))
            {
                documents[e.FullPath] = doc;
                _ = documents.Remove(e.OldFullPath);
            }
            //no need to provide a new catalog, though, since the contents are still effectively the same
        };
        private void PoFileChanged(object sender, FileSystemEventArgs e) => EditorApplication.delayCall += () =>
        {
            bool needNewCatalog = true;
            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                needNewCatalog = documents.Remove(e.FullPath);
            }
            else if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Changed)
            {
                documents[e.FullPath] = GetTextDocument.LoadFrom(e.FullPath);
            }
            else needNewCatalog = false;

            if (needNewCatalog)
            {
                ReloadCatalog();
            }
        };

        //when the language is changed, we get a domain reload
        //so tracking language changes requires persisting it somewhere and looking it up, all from a cctor
        [InitializeOnLoadMethod]
        private static void UnityInitialize()
        {
            Type localizationDatabaseType = Type.GetType("UnityEditor.LocalizationDatabase, UnityEditor");

            const string editorLanguageKey = "EZUtils.Localization.TrackedUnityEditorLanguage";
            string previouslySetEditorLanguage = EditorPrefs.GetString(editorLanguageKey);

            PropertyInfo editorlanguageProperty = localizationDatabaseType.GetProperty(
                "currentEditorLanguage", BindingFlags.Public | BindingFlags.Static);
            SystemLanguage currentEditorLanguage = (SystemLanguage)editorlanguageProperty.GetValue(null);

            if (previouslySetEditorLanguage != currentEditorLanguage.ToString())
            {
                EditorPrefs.SetString(editorLanguageKey, currentEditorLanguage.ToString());
                MethodInfo getCultureMethod = localizationDatabaseType.GetMethod(
                    "GetCulture", BindingFlags.Public | BindingFlags.Static);
                newlySelectedUnityEditorLanguage = CultureInfo.GetCultureInfo(
                    (string)getCultureMethod.Invoke(null, new object[] { currentEditorLanguage }));
            }
            else newlySelectedUnityEditorLanguage = null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    fsw.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
