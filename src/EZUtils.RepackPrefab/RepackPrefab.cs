namespace EZUtils.RepackPrefab
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    using Object = UnityEngine.Object;

    //TODO: perhaps ditch static
    public static class RepackPrefab
    {
        public static GameObject Repack(GameObject sourceObject, GameObject basePrefab)
        {
            bool exceptionCaught = false;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                if (!AssetDatabase.Contains(basePrefab)) throw new InvalidOperationException(
                    $"{nameof(basePrefab)} '{basePrefab.name}' is not a prefab.");

                Dictionary<Object, Object> sourceToNewMap = new Dictionary<Object, Object>();
                GameObject newPrefab = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
                GameObjectUtility.EnsureUniqueNameForSibling(newPrefab);
                Undo.RegisterCreatedObjectUndo(newPrefab, $"Created new instance of '{basePrefab.name}'");

                Copy(sourceObject, newPrefab, sourceToNewMap);

                ReplaceObjectReferences(newPrefab, sourceToNewMap);

                string basePrefabPath = AssetDatabase.GetAssetPath(basePrefab);
                string path = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(
                        Path.GetDirectoryName(basePrefabPath),
                        $"{Path.GetFileNameWithoutExtension(basePrefabPath)} Variant.prefab"));
                _ = PrefabUtility.SaveAsPrefabAssetAndConnect(newPrefab, path, InteractionMode.AutomatedAction);

                return newPrefab;
            }
            catch when ((exceptionCaught = true) != true) //maintain stack
            {
                //we could revert the undo group, but it may be useful for debugging to leave it
                //and the user has options, including undoing manually or fixing the issue if user-generated
                //though for finalizing assets we prefer user-generated issues to be impossible
                throw new InvalidOperationException("literally impossible but gets rid of a warning");
            }
            finally
            {
                Undo.SetCurrentGroupName(exceptionCaught
                    ? $"Failed prefab repack of '{sourceObject.name}' against '{basePrefab.name}'"
                    : $"Prefab repack of '{sourceObject.name}' against '{basePrefab.name}'");
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static void Copy(GameObject reference, GameObject target, Dictionary<Object, Object> sourceToNewMap)
        {
            sourceToNewMap.Add(reference, target);

            Component[] existingComponents = target.GetComponents<Component>();
            //we have a checkpoint under the assumption that reference was originally a modification of target
            //and did relatively sane modifications and didnt mix up ordering.
            //we could hypothetically find the first unmatched component for the given type,
            //but things could end up looking weird in some cases, though perhaps desirable in others.
            int checkpointIndex = 0;
            List<Component> matchedComponents = new List<Component>();
            foreach (Component referenceComponent in reference.GetComponents<Component>())
            {
                Component existingComponent = null;
                int index = checkpointIndex;
                while (existingComponent == null && index < existingComponents.Length)
                {
                    existingComponent = existingComponents[index++];
                    if (referenceComponent.GetType() == existingComponent.GetType())
                    {
                        checkpointIndex = index;
                        sourceToNewMap.Add(referenceComponent, existingComponent);
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
                    sourceToNewMap.Add(referenceComponent, newComponent);
                    EditorUtility.CopySerialized(referenceComponent, newComponent);
                    //even though we added a component to target, updating existingComponents is not necessary
                    //after all, we dont need it to be an eligible match for CopySerialized since we just did it
                }
            }

            foreach (Component unmatchedComponent in existingComponents.Where(c => !matchedComponents.Contains(c)))
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

                if (PrefabUtility.IsAnyPrefabInstanceRoot(referenceGameObject))
                {
                    GameObject prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(referenceGameObject);
                    GameObject newTargetChild = (GameObject)PrefabUtility.InstantiatePrefab(
                        prefabRoot, target.transform);
                    newTargetChild.name = referenceGameObject.name;
                    targetToReferenceGameObjects.Add(newTargetChild, referenceGameObject);

                    //InstantiatePrefab only created a new instance of the prefab.
                    //component modifications, added/removed components, and objects have not been copied.
                    //for new objects and components, we already have logic to add and remove them.
                    //though note that true object removal support is only a thing in unity 2022+ apparently.
                    //
                    //for modified components, we of course have that logic as well. however, these changes don't seem
                    //to stick. didn't dig into it, but it's perhaps due to the lack of a
                    //RecordPrefabInstancePropertyModifications call. there isn't a pretty place to make this call,
                    //but Get/SetPropertyModifications works just as well!
                    PrefabUtility.SetPropertyModifications(
                        newTargetChild, PrefabUtility.GetPropertyModifications(referenceGameObject));
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
                    //can't remove gameobjects from a prefab until unity 2022. might normally just deactivate them, but,
                    //for vrc perf rating stuff, removing the components is better
                    ClearAllComponents(gameObject);
                }
                else
                {
                    Copy(referenceGameObject, gameObject, sourceToNewMap);
                }
            }

            void ClearAllComponents(GameObject gameObject)
            {
                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    //cant get rid of the transform ofc
                    //but also dont really have anything better to do with values (pos, etc) than to just leave them be
                    if (component.GetType() == typeof(Transform)) continue;

                    Object.DestroyImmediate(component);
                }
                foreach (GameObject child in gameObject.GetChildren())
                {
                    ClearAllComponents(child);
                }
            }
        }

        private static void ReplaceObjectReferences(GameObject target, Dictionary<Object, Object> sourceToNewMap)
        {
            //should be hard to do synchronized iteration because of prefabs, so not even trying it
            foreach (Component targetComponent in target.GetComponents<Component>())
            {
                using (SerializedObject serializedTarget = new SerializedObject(targetComponent))
                {
                    SerializedProperty targetIterator = serializedTarget.GetIterator();
                    //note: enterChildren really only enters arrays, structs, and whatnot, and doesnt traverse a whole tree
                    //there isn't currently a scenario where we want to traverse a tree of references
                    //note that nextvisible, in manual testing, caused weird issues with skinnedmeshrenderer
                    //dont really understand the issue enough to have a test case, but Next seems to work fine
                    while (targetIterator.Next(enterChildren: true))
                    {
                        if (targetIterator.propertyType != SerializedPropertyType.ObjectReference
                            || targetIterator.objectReferenceValue == null) continue;
                        if (!targetIterator.editable) continue;

                        if (sourceToNewMap.TryGetValue(targetIterator.objectReferenceValue, out Object targetObject))
                        {
                            targetIterator.objectReferenceValue = targetObject;
                        }
                    }

                    _ = serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            foreach (GameObject child in target.GetChildren())
            {
                ReplaceObjectReferences(child, sourceToNewMap);
            }
        }
    }
}
