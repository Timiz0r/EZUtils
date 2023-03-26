namespace EZUtils
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public class ObjectBuilder
    {
        private readonly GameObject root;

        public ObjectBuilder(string rootObjectName)
        {
            root = new GameObject(rootObjectName);
        }

        public ObjectBuilder AddObject(string name) => AddObject(name, _ => { });
        public ObjectBuilder AddObject(string name, Action<ChildObjectBuilder> childObjectBuilder)
        {
            ChildObjectBuilder b = new ChildObjectBuilder(root, new GameObject(name));
            childObjectBuilder(b);
            return this;
        }
        public ObjectBuilder AddObject(GameObject childGameObject) => AddObject(childGameObject, _ => { });
        public ObjectBuilder AddObject(GameObject childGameObject, Action<ChildObjectBuilder> childObjectBuilder)
        {
            ChildObjectBuilder b = new ChildObjectBuilder(root, childGameObject);
            childObjectBuilder(b);
            return this;
        }


        public ObjectBuilder AddComponent<T>(out T component, params Action<T>[] componentConfigurers) where T : Component
        {
            component = root.AddComponent<T>();
            foreach (Action<T> configurer in componentConfigurers)
            {
                configurer(component);
            }
            return this;
        }
        public ObjectBuilder AddComponent<T>(params Action<T>[] componentConfigurers) where T : Component
            => AddComponent(out _, componentConfigurers);

        public GameObject GetObject() => root;

        public GameObject CreatePrefab(string folder) => CreatePrefab(folder, root);

        public static GameObject CreatePrefab(string folder, GameObject gameObject)
        {
            _ = Directory.CreateDirectory(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(folder, $"{gameObject.name} Variant.prefab"));
            return PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, path, InteractionMode.AutomatedAction);
        }
    }
}
