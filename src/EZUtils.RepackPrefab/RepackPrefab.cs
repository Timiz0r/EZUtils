namespace EZUtils.RepackPrefab
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    using Object = UnityEngine.Object;

    //TODO: undo support
    //TODO: make ui sensible
    //TODO: not sure the component erasure goes all the way down the hierarchy
    public static class RepackPrefab
    {
        public static GameObject Repack(GameObject referenceObject, GameObject referencePrefab)
        {
            if (!AssetDatabase.Contains(referencePrefab)) throw new InvalidOperationException(
                $"{nameof(referencePrefab)} '{referencePrefab.name}' is not a prefab.");

            Dictionary<Object, Object> referenceToTargetMap = new Dictionary<Object, Object>();
            GameObject newPrefab = (GameObject)PrefabUtility.InstantiatePrefab(referencePrefab);
            //TODO: do better with names here. also in testutils.
            newPrefab.name = $"{newPrefab.name} Variant";

            Copy(referenceObject, newPrefab, referenceToTargetMap);

            ReplaceObjectReferences(newPrefab, referenceToTargetMap);

            string referencePrefabPath = AssetDatabase.GetAssetPath(referencePrefab);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(
                    Path.GetDirectoryName(referencePrefabPath),
                    $"{Path.GetFileNameWithoutExtension(referencePrefabPath)} Variant.prefab"));
            _ = PrefabUtility.SaveAsPrefabAssetAndConnect(newPrefab, path, InteractionMode.AutomatedAction);

            return newPrefab;
        }

        private static void ReplaceObjectReferences(GameObject target, Dictionary<Object, Object> referenceToTargetMap)
        {
            //should be hard to do synchronized iteration because of prefabs, so not even trying it
            foreach (Component targetComponent in target.GetComponents<Component>())
            {
                using (SerializedObject serializedTarget = new SerializedObject(targetComponent))
                {
                    SerializedProperty targetIterator = serializedTarget.GetIterator();
                    while (targetIterator.Next(enterChildren: true))
                    {
                        if (targetIterator.propertyType != SerializedPropertyType.ObjectReference
                            || targetIterator.objectReferenceValue == null) continue;
                        if (!targetIterator.editable) continue;

                        if (referenceToTargetMap.TryGetValue(targetIterator.objectReferenceValue, out Object targetObject))
                        {
                            targetIterator.objectReferenceValue = targetObject;
                        }
                    }

                    _ = serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            foreach (GameObject child in target.GetChildren())
            {
                ReplaceObjectReferences(child, referenceToTargetMap);
            }
        }

        private static void Copy(
            GameObject reference, GameObject target, Dictionary<Object, Object> referenceToTargetMap)
        {
            referenceToTargetMap.Add(reference, target);

            Component[] existingComponents = target.GetComponents<Component>();
            //we have a checkpoint under the assumption that reference was originally a modification of target
            //and did relatively sane modifications. we could hypothetically find the first unmatched component for
            //the given type, but things could end up looking weird in some cases, or perhaps desirable in others.
            int checkpointIndex = 0;
            List<Component> matchedComponents = new List<Component>();
            foreach (Component referenceComponent in reference.GetComponents<Component>())
            {
                //TODO: add an option to only add components?

                //TODO: remove the two cases of special transform component handling,
                //since generic component copying and parent setting should work just fine
                if (referenceComponent.GetType() == typeof(Transform))
                {
                    Transform existingTransform =
                        (Transform)existingComponents.Single(c => c.GetType() == typeof(Transform));
                    referenceToTargetMap.Add(referenceComponent, existingTransform);
                    matchedComponents.Add(existingTransform);
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
                        referenceToTargetMap.Add(referenceComponent, existingComponent);
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
                    Component newComponent = target.AddComponent(referenceComponent.GetType());
                    referenceToTargetMap.Add(referenceComponent, newComponent);
                    EditorUtility.CopySerialized(referenceComponent, newComponent);
                    //even though we added a component to target, updating existingComponents is not necessary
                    //after all, we dont need it to be an eligible match for CopySerialized since we just did it
                }
            }

            IEnumerable<Component> unmatchedComponents =
                existingComponents.Where(c => !matchedComponents.Contains(c));
            foreach (Component unmatchedComponent in unmatchedComponents)
            {
                Object.DestroyImmediate(unmatchedComponent);
            }

            GameObject[] existingTargetChildren = target.GetChildren().ToArray();
            //key is based on target so we can look up the matching reference later
            Dictionary<GameObject, GameObject> targetToReferenceGameObjects = new Dictionary<GameObject, GameObject>();
            foreach (GameObject referenceGameObject in reference.GetChildren())
            {
                GameObject existingTargetChild =
                    existingTargetChildren.FirstOrDefault(go => go.name == referenceGameObject.name);

                if (existingTargetChild != null)
                {
                    targetToReferenceGameObjects.Add(existingTargetChild, referenceGameObject);
                    continue;
                }
                else if (PrefabUtility.IsAnyPrefabInstanceRoot(referenceGameObject))
                {
                    GameObject prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(referenceGameObject);
                    GameObject newTargetChild = (GameObject)PrefabUtility.InstantiatePrefab(
                        prefabRoot, target.transform);
                    newTargetChild.name = referenceGameObject.name;
                    targetToReferenceGameObjects.Add(newTargetChild, referenceGameObject);

                    //InstantiatePrefab only created a new instance of the prefab. in general, yet to be copied are
                    //component modifications and added/removed components, objects.
                    //for new objects and components, we already have logic to add and remove them.
                    //though note that removal support is only a thing in unity 2022+ apparently.
                    //
                    //for modified components, we of course have that logic as well. however, these changes don't seem
                    //to stick. didn't dig into it, but it's perhaps due to the lack of a
                    //RecordPrefabInstancePropertyModifications call. there isn't a pretty place to make this call,
                    //but Get/SetPropertyModifications works just as well!
                    PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(referenceGameObject);
                    PrefabUtility.SetPropertyModifications(newTargetChild, modifications);
                }
                else
                {
                    //referenceToTargetMap gets updated in the next recursive call, so no need to update here
                    GameObject newTargetChild = Object.Instantiate(referenceGameObject, target.transform);
                    //gets (Clone); dont want
                    newTargetChild.name = referenceGameObject.name;
                    targetToReferenceGameObjects.Add(newTargetChild, referenceGameObject);
                }
            }

            foreach (GameObject gameObject in target.GetChildren())
            {
                if (!targetToReferenceGameObjects.TryGetValue(gameObject, out GameObject referenceGameObject))
                {
                    //in this case, the target -- the modified version we're trying to reflect onto a base prefab --
                    //has a gameobject not on the reference, meaning the reference removed a game object formerly
                    //on the base prefab.
                    //can't remove gameobjects from a prefab. might normally just deactivate them, but,
                    //for vrc perf rating stuff, removing the components is better
                    ClearAllComponents(gameObject);
                }
                else
                {
                    Copy(referenceGameObject, gameObject, referenceToTargetMap);
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
