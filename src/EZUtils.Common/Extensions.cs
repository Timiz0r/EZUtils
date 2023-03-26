namespace EZUtils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    //should this class grow large, would be good to sort methods into different files
    public static class Extensions
    {
        public static IEnumerable<GameObject> GetChildren(this GameObject parent) => Enumerable
            .Range(0, parent.transform.childCount)
            .Select(i => parent.transform.GetChild(i).gameObject);

        public static TypedObjectField<T> Typed<T>(
            this ObjectField objectField, Action<ObjectField> fieldConfigurer = null) where T : UnityEngine.Object
        {
            fieldConfigurer?.Invoke(objectField);
            return new TypedObjectField<T>(objectField);
        }

        public static T WithClasses<T>(this T element, params string[] classes) where T : VisualElement
        {
            foreach (string @class in classes)
            {
                element.AddToClassList(@class);
            }
            return element;
        }
    }
}
