namespace EZUtils.RepackPrefab.Tests
{
    using System;
    using UnityEngine;

    public class ChildObjectBuilder
    {
        private GameObject currentObject;

        public ChildObjectBuilder(GameObject root, GameObject child)
        {
            currentObject = child;
            currentObject.transform.SetParent(root.transform);
        }

        public ChildObjectBuilder AddObject(string name) => AddObject(name, _ => { });
        public ChildObjectBuilder AddObject(string name, Action<ChildObjectBuilder> childObjectBuilder)
        {
            ChildObjectBuilder b = new ChildObjectBuilder(currentObject, new GameObject(name));
            childObjectBuilder(b);
            return this;
        }
        public ChildObjectBuilder AddObject(GameObject childGameObject) => AddObject(childGameObject, _ => { });
        public ChildObjectBuilder AddObject(GameObject childGameObject, Action<ChildObjectBuilder> childObjectBuilder)
        {
            ChildObjectBuilder b = new ChildObjectBuilder(currentObject, childGameObject);
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
