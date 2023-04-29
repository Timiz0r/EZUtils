namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    //previously, we had a dictionary of these so that instances' locales could be synced
    //with the introduction of localeSynchronizationKey and the synchronizer, that dictionary has effectively moved to
    //the synchronizer.
    //
    //hypothetically, this could be merged with the, at time of writing, bare EZLocaliaztion class.
    //this is not done because CatalogLocaleSynchronizer's SetLocale's touching of CatalogReference.Catalog is convenient.
    //we can keep hiding the underlying catalog in EZLocalization this way.
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
        private GetTextCatalog catalog;
        //catalogLocaleSynchronizer does a getstring which needs to be called as late as possible to avoid throws,
        //particularly when opening an editorwindow
        private readonly Lazy<CatalogLocaleSynchronizer> catalogLocaleSynchronizer;

        private bool disposedValue;

        public CatalogReference(string root, Locale nativeLocale, string localeSynchronizationKey)
        {
            this.root = root;
            NativeLocale = nativeLocale;
            catalogLocaleSynchronizer = new Lazy<CatalogLocaleSynchronizer>(
                () => CatalogLocaleSynchronizer.Register(localeSynchronizationKey, this),
                System.Threading.LazyThreadSafetyMode.None);

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

        public Locale NativeLocale { get; }

        public GetTextCatalog Catalog
        {
            get
            {
                if (catalog == null)
                {
                    DirectoryInfo directory = new DirectoryInfo(root);
                    if (directory.Exists)
                    {
                        foreach (FileInfo file in directory.EnumerateFiles("*.po", SearchOption.TopDirectoryOnly))
                        {
                            documents.Add(file.FullName, GetTextDocument.LoadFrom(file.FullName));
                        }
                    }

                    ReloadCatalog();
                    fsw.EnableRaisingEvents = true;
                }

                return catalog;
            }
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
            _ = Catalog;

            catalogLocaleSynchronizer.Value.SelectLocale(locale);
            Retranslate();
        }
        public Locale SelectLocale(CultureInfo cultureInfo)
        {
            EnsureCatalogInitialized();

            Locale locale = catalogLocaleSynchronizer.Value.SelectLocale(cultureInfo);
            Retranslate();
            return locale;
        }

        public bool TrySelectLocale(Locale locale)
        {
            EnsureCatalogInitialized();

            bool result = catalogLocaleSynchronizer.Value.TrySelectLocale(locale);
            Retranslate();
            return result;
        }
        public bool TrySelectLocale(CultureInfo cultureInfo, out Locale correspondingLocale)
        {
            EnsureCatalogInitialized();

            bool result = catalogLocaleSynchronizer.Value.TrySelectLocale(cultureInfo, out correspondingLocale);
            Retranslate();
            return result;
        }
        public bool TrySelectLocale(CultureInfo cultureInfo) => TrySelectLocale(cultureInfo, out _);

        public Locale SelectLocaleOrNative(Locale[] locales)
        {
            EnsureCatalogInitialized();

            Locale locale = catalogLocaleSynchronizer.Value.SelectLocaleOrNative(locales);
            Retranslate();
            return locale;
        }
        public Locale SelectLocaleOrNative(CultureInfo[] cultureInfos)
        {
            EnsureCatalogInitialized();

            Locale locale = catalogLocaleSynchronizer.Value.SelectLocaleOrNative(cultureInfos);
            Retranslate();
            return locale;
        }
        public Locale SelectLocaleOrNative() => SelectLocaleOrNative(Array.Empty<Locale>());

        //there's a bit of a circular reference issue that happens whenn
        //catalogLocaleSynchronizer is called before Catalog is initialized,
        //since catalog initialization depends on getting the initial locale from the synchronizer
        private void EnsureCatalogInitialized() => _ = Catalog;

        private void ReloadCatalog()
        {
            catalog = new GetTextCatalog(documents.Values.ToArray(), NativeLocale);
            //we dont call our methods because, if the synchronized locale isnt in this catalog, we dont want to
            //proparate a change just based on this. we want a deliberate user action to change it.
            _ = catalog.SelectLocaleOrNative(catalogLocaleSynchronizer.Value.SelectedLocale);
            Retranslate();
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
                documents[e.FullPath] = GetTextDocument.LoadFrom(e.FullPath);
            }
            else needNewCatalog = false;

            if (needNewCatalog)
            {
                ReloadCatalog();
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
