namespace EZUtils.RepackPrefab
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public static class Extensions
    {
        public static IEnumerable<GameObject> GetChildren(this GameObject parent) => Enumerable
            .Range(0, parent.transform.childCount)
            .Select(i => parent.transform.GetChild(i).gameObject);
    }
}
