namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.IO.Pem;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;
    using static UnityEngine.UI.Image;
    using Object = UnityEngine.Object;

    //is not static to allow workflows where these loaded assets are then modified.
    //would not one consumer to affect another consumer.
    public class VrcDefaultAnimatorControllers
    {
        //using asset ids to be vcc/old unitypackage agnostic
        private readonly Lazy<AnimatorController> @base =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3LocomotionLayer.controller"));
        private readonly Lazy<AnimatorController> additive =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3IdleLayer.controller"));
        private readonly Lazy<AnimatorController> gesture =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3HandsLayer.controller"));
        private readonly Lazy<AnimatorController> action =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3ActionLayer.controller"));
        private readonly Lazy<AnimatorController> fx =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3HandsLayer.controller"));
        private readonly Lazy<AnimatorController> sitting =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3SittingLayer.controller"));
        private readonly Lazy<AnimatorController> tPose =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3UtilityTPose.controller"));
        private readonly Lazy<AnimatorController> ikPose =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3UtilityIKPose.controller"));

        public AnimatorController Base => @base.Value;
        public AnimatorController Additive => additive.Value;
        public AnimatorController Gesture => gesture.Value;
        public AnimatorController Action => action.Value;
        public AnimatorController FX => fx.Value;
        public AnimatorController Sitting => sitting.Value;
        public AnimatorController TPose => tPose.Value;
        public AnimatorController IKPose => ikPose.Value;

        private static AnimatorController Load(string fileName)
        {
            AnimatorController original = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                $"Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers/{fileName}");
            if (original == null)
            {
                original = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    $"Assets/Samples/VRChat SDK - Avatars/3.0.6/AV3 Demo Assets/Animation/Controllers/{fileName}");
            }

            if (original == null) throw new InvalidOperationException($"Could not find asset {fileName}'.");

            return DeepCopy(original);
        }

        //TODO: needs pretty heavy testing.
        //could also try to create a more generic version using SO more. the hard part is know what to copy.
        //for instance, we dont copy motions or avatarmasks, but they would certainly be object references.
        //maybe checking assetdatabase to see if it's a subasset would work.
        private static AnimatorController DeepCopy(AnimatorController original)
        {

            Dictionary<Object, Object> objectMap = new Dictionary<Object, Object>();
            HashSet<Object> traversedObjects = new HashSet<Object>();
            AnimatorController copy = Copy(original);

            AnimatorControllerLayer[] layers = original.layers;
            foreach (AnimatorControllerLayer layer in layers)
            {
                layer.stateMachine = CopyStateMachine(layer.stateMachine);
            }

            SwapReferences(copy);

            void SwapReferences(Object target)
            {
                if (traversedObjects.Contains(target)) return;
                _ = traversedObjects.Add(target);

                SerializedObject so = new SerializedObject(target);
                SerializedProperty targetIterator = so.GetIterator();
                while (targetIterator.Next(enterChildren: true))
                {
                    if (targetIterator.propertyType != SerializedPropertyType.ObjectReference
                        || targetIterator.objectReferenceValue == null) continue;

                    System.Type objectReferenceType = targetIterator.objectReferenceValue.GetType();
                    if (objectReferenceType != typeof(AnimatorState)
                        && objectReferenceType != typeof(AnimatorStateMachine)
                        && objectReferenceType != typeof(AnimatorController)
                        && objectReferenceType != typeof(AnimatorTransition)) continue;

                    if (objectMap.TryGetValue(targetIterator.objectReferenceValue, out Object newObject))
                    {
                        SwapReferences(targetIterator.objectReferenceValue);
                        targetIterator.objectReferenceValue = newObject;
                    }
                    else if (objectReferenceType == typeof(AnimatorController))
                    {
                        targetIterator.objectReferenceValue = copy;
                    }
                    else throw new InvalidOperationException("Didn't find a match yo.");
                }
            }

            return copy;

            AnimatorStateMachine CopyStateMachine(AnimatorStateMachine originalStateMachine)
            {
                //cannot object.instantiate AnimatorStateMachine due to an assertion of unknown cause 🤷‍
                AnimatorStateMachine newStateMachine = new AnimatorStateMachine()
                {
                    anyStatePosition = originalStateMachine.anyStatePosition,
                    name = originalStateMachine.name,
                    anyStateTransitions = CopyAll(originalStateMachine.anyStateTransitions),
                    entryTransitions = CopyAll(originalStateMachine.entryTransitions),
                    states = originalStateMachine.states
                        .Select(cas => new ChildAnimatorState()
                        {
                            position = cas.position,
                            state = CopyState(cas.state)
                        })
                        .ToArray(),
                    stateMachines = originalStateMachine.stateMachines
                        .Select(casm => new ChildAnimatorStateMachine()
                        {
                            position = casm.position,
                            stateMachine = CopyStateMachine(casm.stateMachine)
                        })
                        .ToArray(),
                    behaviours = CopyAll(originalStateMachine.behaviours),
                    defaultState = originalStateMachine.defaultState,
                    entryPosition = originalStateMachine.entryPosition,
                    exitPosition = originalStateMachine.exitPosition,
                    hideFlags = originalStateMachine.hideFlags,
                    parentStateMachinePosition = originalStateMachine.parentStateMachinePosition,
                };

                objectMap[originalStateMachine] = newStateMachine;
                return newStateMachine;
            }

            AnimatorState CopyState(AnimatorState originalState)
            {
                AnimatorState newState = Copy(originalState);
                newState.transitions = CopyAll(newState.transitions);
                newState.behaviours = CopyAll(newState.behaviours);
                return newState;
            }

            T Copy<T>(T originalObject) where T : Object
            {
                T newObject = Object.Instantiate(originalObject);
                newObject.name = originalObject.name;
                objectMap[originalObject] = newObject;
                return newObject;
            }
            T[] CopyAll<T>(T[] originalObjects) where T : Object
                => originalObjects.Select(o => Copy(o)).ToArray();
        }
    }
}
