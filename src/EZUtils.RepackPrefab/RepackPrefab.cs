namespace EZUtils.RepackPrefab
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public static class RepackPrefab
    {
        public static GameObject Repack(GameObject referenceObject, GameObject referencePrefab)
        {
            GameObject newPrefab = (GameObject)PrefabUtility.InstantiatePrefab(referencePrefab);

            Copy(referenceObject, newPrefab);

            string referencePrefabPath = AssetDatabase.GetAssetPath(referencePrefab);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(
                    Path.GetDirectoryName(referencePrefabPath),
                    $"{Path.GetFileNameWithoutExtension(referencePrefabPath)} Variant.prefab"));
            _ = PrefabUtility.SaveAsPrefabAssetAndConnect(newPrefab, path, InteractionMode.AutomatedAction);

            return newPrefab;
        }

        private static void Copy(GameObject reference, GameObject target)
        {
            Component[] existingComponents = target.GetComponents<Component>();
            int checkpointIndex = 0;
            List<Component> matchedComponents = new List<Component>();
            foreach (Component referenceComponent in reference.GetComponents<Component>())
            {
                //TODO: add logic for known vrc components like pb
                //the problem with handling components is that they have no identifier, though specific components may
                //(such as pb components and their target bones/objects)
                //perhaps the best automatic thing we can do is assume the order was never changed and add/remove
                //based on the components' types we encounter
                //TODO: add an option to only add components
                //TODO: probably not a useful enough tool to do it, but add ux to allow users to resolve issues manually
                //TODO: for the copying we do, find a matching object reference. probably needs to be done in a second pass,
                //since it may depend on gameobjects that do not yet exist.

                //TODO: move the other logic we have for transform component here
                //will prob do it around when we support other component types
                if (referenceComponent.GetType() == typeof(Transform))
                {
                    matchedComponents.Add(existingComponents.First(c => c.GetType() == typeof(Transform)));
                    continue;
                }

                Component existingComponent = null;
                int index = checkpointIndex;
                while (existingComponent == null && index < existingComponents.Length)
                {
                    existingComponent = existingComponents[index++];
                    if (referenceComponent.GetType() == existingComponent.GetType())
                    {
                        checkpointIndex = index;
                        matchedComponents.Add(existingComponent);

                        EditorUtility.CopySerialized(referenceComponent, existingComponent);
                    }
                    else
                    {
                        existingComponent = null;
                    }
                }
                if (existingComponent == null)
                {
                    EditorUtility.CopySerialized(
                        referenceComponent,
                        target.AddComponent(referenceComponent.GetType()));
                    //updating existingComponents is not necessary or desired, since it should never come up as a match
                    //from reference components
                }
            }

            IEnumerable<Component> unmatchedComponents =
                existingComponents.Where(c => !matchedComponents.Contains(c));
            foreach (Component unmatchedComponent in unmatchedComponents)
            {
                Object.DestroyImmediate(unmatchedComponent);
            }

            GameObject[] existingGameObjects = target.GetChildren().ToArray();
            //key is based on target so we can look up the matching reference later
            Dictionary<GameObject, GameObject> matchedGameObjects = new Dictionary<GameObject, GameObject>();
            foreach (GameObject referenceGameObject in reference.GetChildren())
            {
                GameObject existingGameObject =
                    existingGameObjects.FirstOrDefault(go => go.name == referenceGameObject.name);

                if (existingGameObject == null)
                {
                    GameObject newGameObject = new GameObject(referenceGameObject.name);
                    EditorUtility.CopySerialized(referenceGameObject.transform, newGameObject.transform);
                    newGameObject.transform.SetParent(target.transform);
                }
                else
                {
                    matchedGameObjects.Add(existingGameObject, referenceGameObject);
                }
            }

            foreach (GameObject gameObject in target.GetChildren())
            {
                if (!matchedGameObjects.TryGetValue(gameObject, out GameObject referenceGameObject))
                {
                    //can't remove gameobjects from a prefab. might normally just deactivate them, but,
                    //for vrc perf rating stuff, removing the components is better
                    ClearAllComponents(gameObject);
                }
                else
                {
                    Copy(referenceGameObject, gameObject);
                }
            }

            void ClearAllComponents(GameObject gameObject)
            {
                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    //cant get rid of the transform ofc, but also dont really have anything better to do with values
                    //than to just leave them be
                    if (component.GetType() == typeof(Transform)) continue;

                    Object.DestroyImmediate(component);
                }
                foreach (GameObject child in gameObject.GetChildren())
                {
                    ClearAllComponents(child);
                }
            }
        }
    }
}
