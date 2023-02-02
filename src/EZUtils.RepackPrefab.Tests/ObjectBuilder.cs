namespace EZUtils.RepackPrefab.Tests
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public class ObjectBuilder
    {
        private GameObject root;

        public ObjectBuilder(string rootObjectName)
        {
            root = new GameObject(rootObjectName);
        }

        public ObjectBuilder AddObject(string name) => AddObject(name, _ => { });
        public ObjectBuilder AddObject(string name, Action<ChildObjectBuilder> childObjectBuilder)
        {
            ChildObjectBuilder b = new ChildObjectBuilder(root, name);
            childObjectBuilder(b);
            return this;
        }

        public ObjectBuilder AddComponent<T>() where T : Component => AddComponent<T>(_ => { });
        public ObjectBuilder AddComponent<T>(Action<T> componentBuilder) where T : Component
        {
            T component = root.AddComponent<T>();
            componentBuilder(component);
            return this;
        }

        public GameObject GetObject() => root;

        public GameObject CreatePrefab() => TestUtils.CreatePrefab(root);
    }
}
