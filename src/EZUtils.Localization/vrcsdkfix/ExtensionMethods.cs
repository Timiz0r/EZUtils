using System;
using System.Collections.Generic;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEditor.Build.Player;
using UnityEditor;

//VRCSDK is an autoreferenced plugin that has a globally namespaced ExtensionMethods class
//scriptablebuildpipeline ends up using that ExtensionMethods
//to fix this, we use an assembly definition reference to add this globally namespaced ExtensionMethods class to it
//which then calls the right ExtensionMethods class
static class ExtensionMethods
{
    public static bool IsNullOrEmpty<T>(this ICollection<T> collection) => UnityEditor.Build.Pipeline.Utilities.ExtensionMethods.IsNullOrEmpty(collection);

    public static void GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TValue value) where TValue : new()
        => UnityEditor.Build.Pipeline.Utilities.ExtensionMethods.GetOrAdd(dictionary, key, out value);

    public static void Swap<T>(this IList<T> list, int first, int second) => UnityEditor.Build.Pipeline.Utilities.ExtensionMethods.Swap(list, first, second);

    public static void GatherSerializedObjectCacheEntries(this WriteCommand command, HashSet<CacheEntry> cacheEntries)
        => UnityEditor.Build.Pipeline.Utilities.ExtensionMethods.GatherSerializedObjectCacheEntries(command, cacheEntries);

    public static void ExtractCommonCacheData(IBuildCache cache, IEnumerable<ObjectIdentifier> includedObjects, IEnumerable<ObjectIdentifier> referencedObjects, HashSet<Type> uniqueTypes, List<ObjectTypes> objectTypes, HashSet<CacheEntry> dependencies)
        => UnityEditor.Build.Pipeline.Utilities.ExtensionMethods.ExtractCommonCacheData(cache, includedObjects, referencedObjects, uniqueTypes, objectTypes, dependencies);

#if NONRECURSIVE_DEPENDENCY_DATA
    public static ObjectIdentifier[] FilterReferencedObjectIDs(GUID asset, ObjectIdentifier[] references, BuildTarget target, TypeDB typeDB, HashSet<GUID> dependencies)
        => UnityEditor.Build.Pipeline.Utilities.ExtensionMethods.FilterReferencedObjectIDs(asset, references, target, typeDB, dependencies);

#endif
}
