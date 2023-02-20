namespace EZUtils.TestUtils
{
    using System.Linq;
    using UnityEngine;

    public static class TestUtils
    {
        public static void ClearScene()
        {
            //not sure if it's the newer UTF or not, but the gameobject destruction does not appear to be needed
            //the asset deletion, however, is still needed
            GameObject dummy = new GameObject();
            foreach (GameObject obj in dummy.scene.GetRootGameObjects().Cast<GameObject>())
            {
                if (obj.name == "tests runner") continue;
                Object.DestroyImmediate(obj);
            }
        }
    }
}
