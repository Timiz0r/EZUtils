namespace EZUtils.Localization
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    //TODO: make a path class that makes maintaining convention easier
    public class CatalogDatabase : AssetPostprocessor
    {
        private static readonly Dictionary<string, GetTextDocument> documents = new Dictionary<string, GetTextDocument>();

        private static readonly Dictionary<string, CatalogReference> catalogs = new Dictionary<string, CatalogReference>();

        //
        public static CatalogReference GetCatalogReference(string root, CultureInfo nativeLocale)
        {
            root = root.Replace('\\', '/');
            if (catalogs.TryGetValue(root, out CatalogReference catalogReference))
            {
                //we could check to see that native locales match, but it's not that important
                return catalogReference;
            }

            catalogReference = catalogs[root] = new CatalogReference(nativeLocale);
            GetTextCatalog catalog = GetFreshCatalog(root, nativeLocale);
            catalogReference.UseUpdatedCatalog(catalog);
            return catalogReference;
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            string[] addedPaths = importedAssets
                .Concat(movedAssets)
                .Where(p => p.EndsWith(".po", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            string[] removedPaths = deletedAssets
                .Concat(movedFromAssetPaths)
                .Where(p => p.EndsWith(".po", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            string[] allPaths = addedPaths.Concat(removedPaths).ToArray();

            foreach (string path in removedPaths)
            {
                _ = documents.Remove(path);
            }

            foreach (string path in addedPaths)
            {
                using (StreamReader sr = new StreamReader(Path.GetFullPath(path)))
                {
                    //note that modifications are also covered by importedAssets, which works fine for us
                    try
                    {
                        documents[path] = GetTextDocument.LoadFrom(sr);
                    }
                    catch (Exception ex)
                    {
                        //dont want to stop all loading just on a single doc
                        Debug.LogException(ex);
                    }
                }
            }

            RefreshRequiredCatalogs(allPaths);
        }

        private static void RefreshRequiredCatalogs(string[] modifiedPaths)
        {
            foreach ((string root, CatalogReference catalogReference) in catalogs.Select(kvp => (kvp.Key, kvp.Value)))
            {
                if (!modifiedPaths.Any(p => IsRootedUnder(root, p))) continue;

                GetTextCatalog catalog = GetFreshCatalog(root, catalogReference.NativeLocale);
                catalogReference.UseUpdatedCatalog(catalog);
            }
        }

        private static GetTextCatalog GetFreshCatalog(string root, CultureInfo nativeLocale) => new GetTextCatalog(
            documents
                .Where(kvp => kvp.Key.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Value)
                .ToArray(),
            nativeLocale);

        private static bool IsRootedUnder(string root, string path)
            => Path.GetDirectoryName(path).Replace('\\', '/').Equals(root, StringComparison.OrdinalIgnoreCase);

        [InitializeOnLoadMethod]
        private static void LoadExistingDocuments()
        {
            //dont ultimately hit issues using assetdatabase in a InitializeOnLoadMethod
            //we cant statically initialize tho because unity wont let us
            string[] paths = AssetDatabase.FindAssets("t:LocalizationAsset")
                .Select(id => AssetDatabase.GUIDToAssetPath(id))
                //have no idea if all LocalizationAssets are .po files
                .Where(p => p.EndsWith(".po", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (string path in paths)
            {
                using (StreamReader sr = new StreamReader(Path.GetFullPath(path)))
                {
                    try
                    {
                        documents[path] = GetTextDocument.LoadFrom(sr);
                    }
                    catch (Exception e)
                    {
                        //don't want to fail loading otherwise fine documents
                        Debug.LogException(e);
                    }
                }
            }

            RefreshRequiredCatalogs(paths);
        }
    }
}
