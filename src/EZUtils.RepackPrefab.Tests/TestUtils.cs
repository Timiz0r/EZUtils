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
            //not sure if it's the newer UTF or not, but the gameobject destruction does not appear to be needed
            //the asset deletion, however, is still needed
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
                Path.Combine(TestArtifactRootFolder, $"{gameObject.name} Variant.prefab"));
            return PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, path, InteractionMode.AutomatedAction);
        }
    }
}
