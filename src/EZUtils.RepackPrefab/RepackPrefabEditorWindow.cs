namespace EZUtils.RepackPrefab
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class RepackPrefabEditorWindow : EditorWindow
    {
        [MenuItem("EZUtils/Repack prefab", isValidateFunction: false, priority: 0)]
        public static void PackageManager()
        {
            RepackPrefabEditorWindow window = GetWindow<RepackPrefabEditorWindow>("Repack prefab");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.repackprefab/RepackPrefabEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            ObjectField referenceObject = rootVisualElement.Q<ObjectField>(name: "referenceObject");
            referenceObject.objectType = typeof(GameObject);

            ObjectField referencePrefab = rootVisualElement.Q<ObjectField>(name: "referencePrefab");
            referencePrefab.objectType = typeof(GameObject);

            //TODO: don't allow the button to be clicked unless objectfields valid
            rootVisualElement.Q<Button>(name: "repackPrefab").clicked += () =>
            {
                GameObject newPrefab = (GameObject)PrefabUtility.InstantiatePrefab((GameObject)referencePrefab.value);

                Copy((GameObject)referenceObject.value, newPrefab);
            };
        }

        private static void Copy(GameObject reference, GameObject target)
        {
            MonoBehaviour[] existingComponents = target.GetComponents<MonoBehaviour>();
            int checkpointIndex = 0;
            List<MonoBehaviour> matchedComponents = new List<MonoBehaviour>();
            foreach (MonoBehaviour referenceComponent in reference.GetComponents<MonoBehaviour>())
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

                MonoBehaviour existingComponent = null;
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

            IEnumerable<MonoBehaviour> unmatchedComponents =
                existingComponents.Where(c => !matchedComponents.Contains(c));
            foreach (MonoBehaviour unmatchedComponent in unmatchedComponents)
            {
                DestroyImmediate(unmatchedComponent);
            }

            GameObject[] existingGameObjects = GetChildren(target).ToArray();
            //key is based on target so we can look up the matching reference later
            Dictionary<GameObject, GameObject> matchedGameObjects = new Dictionary<GameObject, GameObject>();
            foreach (GameObject referenceGameObject in GetChildren(reference))
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

            foreach (GameObject gameObject in GetChildren(target))
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

            IEnumerable<GameObject> GetChildren(GameObject parent) => Enumerable
                .Range(0, parent.transform.childCount)
                .Select(i => parent.transform.GetChild(i).gameObject);
            void ClearAllComponents(GameObject gameObject)
            {
                foreach (MonoBehaviour component in gameObject.GetComponents<MonoBehaviour>())
                {
                    //cant get rid of the transform ofc, but also dont really have anything better to do with values
                    //than to just leave them be
                    if (component.GetType() == typeof(Transform)) continue;

                    DestroyImmediate(component);
                }
                foreach (GameObject child in GetChildren(gameObject))
                {
                    ClearAllComponents(child);
                }
            }
        }
    }
}
