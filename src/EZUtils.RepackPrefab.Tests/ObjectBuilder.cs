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

        public ObjectBuilder AddComponent<T>() where T : Component => AddComponent<T>(_ => { });
        public ObjectBuilder AddComponent<T>(Action<T> componentBuilder) where T : Component
        {
            T component = root.AddComponent<T>();
            componentBuilder(component);
            return this;
        }

        public GameObject GetObject() => root;

        public GameObject CreatePrefab() => CreatePrefab(root);

        public static GameObject CreatePrefab(GameObject gameObject)
        {
            _ = Directory.CreateDirectory(RepackPrefabTests.TestArtifactRootFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(RepackPrefabTests.TestArtifactRootFolder, $"{gameObject.name} Variant.prefab"));
            return PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, path, InteractionMode.AutomatedAction);
        }
    }
}
