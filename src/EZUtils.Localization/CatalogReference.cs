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
        private bool initialized = false;
        private bool disposedValue;

        public Locale NativeLocale { get; }
        public Locale IntendedLocale { get; private set; }
        public Locale CurrentLocale { get; private set; }
        public IReadOnlyList<Locale> SupportedLocales => Catalog.SupportedLocales;
        public GetTextCatalog Catalog { get; private set; }

        public CatalogReference(
            string root,
            Locale nativeLocale)
        {
            this.root = root;
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
            if (initialized) return;

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

            initialized = true;
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
        //the locale synchronizer will generally deal with the catalog directly,
        //but, the reference still needs to be informed in case po files change
        public void RecordCurrentLocale(Locale currentLocale)
        {
            CurrentLocale = currentLocale;
            Retranslate();
        }

        public void RecordIntendedLocale(Locale intendedLocale) => IntendedLocale = intendedLocale;

        private void ReloadCatalog()
        {
            Catalog = new GetTextCatalog(documents.Values.ToArray(), NativeLocale);
            //if the synchronizer picked a locale we don't support, we still want to try it on reloads
            //in case the necessary document was added
            _ = Catalog.SelectLocaleOrNative(IntendedLocale, CurrentLocale);
            UIElements.LocaleSelectionUI.ReportChange();
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
