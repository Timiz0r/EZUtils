namespace EZUtils
{
    using System;
    using UnityEngine;

    public class ChildObjectBuilder
    {
        private readonly GameObject currentObject;

        public ChildObjectBuilder(GameObject root, GameObject child)
        {
            currentObject = child;
            if (root != null)
            {
                currentObject.transform.SetParent(root.transform);
            }
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

        public ChildObjectBuilder AddComponent<T>(out T component, params Action<T>[] componentConfigurers) where T : Component
        {
            component = currentObject.AddComponent<T>();
            foreach (Action<T> configurer in componentConfigurers)
            {
                configurer(component);
            }
            return this;
        }
        public ChildObjectBuilder AddComponent<T>(params Action<T>[] componentConfigurers) where T : Component
            => AddComponent(out _, componentConfigurers);
    }
}
