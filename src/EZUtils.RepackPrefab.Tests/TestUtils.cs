namespace EZUtils.RepackPrefab.Tests
{
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
    }
}
