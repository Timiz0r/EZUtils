namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    internal class CatalogReference : IDisposable
    {
        private readonly List<(Object obj, Action action)> retranslatableObjects =
            new List<(Object obj, Action action)>();
        private readonly List<(VisualElement element, Action action)> retranslatableElements =
            new List<(VisualElement element, Action action)>();

        //not sure if it's better to watch all subdirs from the project root, or to have one watcher per provided root
        //am guessing this will be better, since all of the temp folders and whatnot are pretty large
        private readonly FileSystemWatcher fsw;
        //key is full path to make the initial load and fsw paths the same, in the easiest way possible
        private readonly Dictionary<string, GetTextDocument> documents = new Dictionary<string, GetTextDocument>();

        private readonly string root;
        private readonly CatalogLocaleSynchronizer catalogLocaleSynchronizer;
        private bool disposedValue;

        public Locale NativeLocale { get; }
        public IReadOnlyList<Locale> SupportedLocales => Catalog.SupportedLocales;
        public GetTextCatalog Catalog { get; private set; }

        public CatalogReference(
            string root,
            Locale nativeLocale,
            CatalogLocaleSynchronizer catalogLocaleSynchronizer)
        {
            this.root = root;
            this.catalogLocaleSynchronizer = catalogLocaleSynchronizer;
            NativeLocale = nativeLocale;
            Catalog = new GetTextCatalog(Array.Empty<GetTextDocument>(), nativeLocale);

            fsw = new FileSystemWatcher()
            {
                Path = root,
                Filter = "*.po",
                IncludeSubdirectories = false,
            };
            fsw.Changed += PoFileChanged;
            fsw.Created += PoFileChanged;
            fsw.Deleted += PoFileChanged;
            fsw.Renamed += PoFileRenamed;
        }

        public void Initialize()
        {
            //this has an initialization method mainly because, as an implementation detail of EZLocalization,
            //ReloadCatalog's usage of the synchronizer must only happen after the synchronizer is given
            //an instance of this.
            DirectoryInfo directory = new DirectoryInfo(root);
            if (directory.Exists)
            {
                foreach (FileInfo file in directory.EnumerateFiles("*.po", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        documents.Add(file.FullName, GetTextDocument.LoadFrom(file.FullName));
                    }
                    catch (Exception ex) when (ExceptionUtil.Record(() => Debug.LogException(ex)))
                    {
                    }
                }
            }
            ReloadCatalog();
            fsw.EnableRaisingEvents = true;
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
        public void TrackRetranslatable(Object obj, Action action)
        {
            retranslatableObjects.Add((obj, action));
            action();
        }
        public void TrackRetranslatable(VisualElement element, Action action)
        {
            retranslatableElements.Add((element, action));
            action();
        }

        public void SelectLocale(Locale locale)
        {
            catalogLocaleSynchronizer.SelectLocale(locale);
            Retranslate();
        }
        public Locale SelectLocale(CultureInfo cultureInfo)
        {
            Locale locale = catalogLocaleSynchronizer.SelectLocale(cultureInfo);
            Retranslate();
            return locale;
        }

        public bool TrySelectLocale(Locale locale)
        {
            bool result = catalogLocaleSynchronizer.TrySelectLocale(locale);
            Retranslate();
            return result;
        }
        public bool TrySelectLocale(CultureInfo cultureInfo, out Locale correspondingLocale)
        {
            bool result = catalogLocaleSynchronizer.TrySelectLocale(cultureInfo, out correspondingLocale);
            Retranslate();
            return result;
        }
        public bool TrySelectLocale(CultureInfo cultureInfo) => TrySelectLocale(cultureInfo, out _);

        public Locale SelectLocaleOrNative(Locale[] locales)
        {
            Locale locale = catalogLocaleSynchronizer.SelectLocaleOrNative(locales);
            Retranslate();
            return locale;
        }
        public Locale SelectLocaleOrNative(CultureInfo[] cultureInfos)
        {
            Locale locale = catalogLocaleSynchronizer.SelectLocaleOrNative(cultureInfos);
            Retranslate();
            return locale;
        }
        public Locale SelectLocaleOrNative() => SelectLocaleOrNative(Array.Empty<Locale>());

        private void ReloadCatalog()
        {
            Catalog = new GetTextCatalog(documents.Values.ToArray(), NativeLocale);
            //we dont call our methods because, if the synchronized locale isnt in this catalog, we dont want to
            //proparate a change just based on this. we want a deliberate user action to change it.
            _ = Catalog.SelectLocaleOrNative(catalogLocaleSynchronizer.SelectedLocale);
            catalogLocaleSynchronizer.UI.ReportChange();
        }

        //NOTE: fsw is not thread-safe by default in two ways:
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
                try
                {
                    documents[e.FullPath] = GetTextDocument.LoadFrom(e.FullPath);
                }
                catch (Exception ex) when (ExceptionUtil.Record(() => Debug.LogException(ex)))
                {
                }
            }
            else needNewCatalog = false;

            if (needNewCatalog)
            {
                ReloadCatalog();
                Retranslate();
            }
        };

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
