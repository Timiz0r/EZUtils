namespace EZUtils.RepackPrefab.Tests
{
    using System;
    using UnityEngine;

    public class ChildObjectBuilder
    {
        private GameObject currentObject;

        public ChildObjectBuilder(GameObject root, string name)
        {
            GameObject currentObject = new GameObject(name);
            currentObject.transform.SetParent(root.transform);
        }

        public ChildObjectBuilder AddObject(string name) => AddObject(name, _ => { });
        public ChildObjectBuilder AddObject(string name, Action<ChildObjectBuilder> childObjectBuilder)
        {
            ChildObjectBuilder b = new ChildObjectBuilder(currentObject, name);
            childObjectBuilder(b);
            return this;
        }

        public ChildObjectBuilder AddComponent<T>() where T : Component => AddComponent<T>(_ => { });
        public ChildObjectBuilder AddComponent<T>(Action<T> componentBuilder) where T : Component
        {
            T component = currentObject.AddComponent<T>();
            componentBuilder(component);
            return this;
        }
    }
}
