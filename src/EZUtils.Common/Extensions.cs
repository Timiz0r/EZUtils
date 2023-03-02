namespace EZUtils
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor.UIElements;
    using UnityEngine;

    public static class Extensions
    {
        public static IEnumerable<GameObject> GetChildren(this GameObject parent) => Enumerable
            .Range(0, parent.transform.childCount)
            .Select(i => parent.transform.GetChild(i).gameObject);

        public static TypedObjectField<T> Typed<T>(this ObjectField objectField) where T : Object
            => new TypedObjectField<T>(objectField);
    }
}
