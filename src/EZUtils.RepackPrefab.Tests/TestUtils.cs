namespace EZUtils.RepackPrefab.Tests
{
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public static class TestUtils
    {
        public static readonly string TestArtifactRootFolder = "Assets/RepackPrefabTest";
        public static void StandardTearDown()
        {
            GameObject dummy = new GameObject();
            foreach (GameObject obj in dummy.scene.GetRootGameObjects().Cast<GameObject>())
            {
                Object.DestroyImmediate(obj);
            }

            _ = AssetDatabase.DeleteAsset(TestArtifactRootFolder);
        }

        public static GameObject CreatePrefab(GameObject gameObject)
        {
            _ = Directory.CreateDirectory(TestArtifactRootFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(TestArtifactRootFolder, $"{gameObject.name}.prefab"));
            return PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, path, InteractionMode.AutomatedAction);
        }
    }
}
