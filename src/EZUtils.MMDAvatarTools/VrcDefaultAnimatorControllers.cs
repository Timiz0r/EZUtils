namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        //this still causes innocuous changes, though
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

                    if (objectMap.TryGetValue(targetIterator.objectReferenceValue, out Object newObject))
                    {
                        SwapReferences(targetIterator.objectReferenceValue);
                        targetIterator.objectReferenceValue = newObject;
                    }
                }
                //this actually breaks if done without undo, which is probably a hint as to where to micro changes come from
                _ = so.ApplyModifiedProperties();
            }

            return copy;

            AnimatorStateMachine CopyStateMachine(AnimatorStateMachine originalStateMachine)
            {
                //get asserts when object instantiating state machines
                AnimatorStateMachine newStateMachine = new AnimatorStateMachine();
                EditorUtility.CopySerialized(originalStateMachine, newStateMachine);
                objectMap[originalStateMachine] = newStateMachine;

                newStateMachine.states = CopyAll(
                    originalStateMachine.states, casm => new ChildAnimatorState()
                    {
                        position = casm.position,
                        state = CopyState(casm.state)
                    });
                newStateMachine.stateMachines = CopyAll(
                    originalStateMachine.stateMachines, casm => new ChildAnimatorStateMachine()
                    {
                        position = casm.position,
                        stateMachine = CopyStateMachine(casm.stateMachine)
                    })
                    .ToArray();
                newStateMachine.anyStateTransitions =
                    originalStateMachine.anyStateTransitions.Select(t => Copy(t)).ToArray();
                newStateMachine.entryTransitions =
                    originalStateMachine.entryTransitions.Select(t => Copy(t)).ToArray();

                return newStateMachine;
            }

            AnimatorState CopyState(AnimatorState originalState)
            {
                AnimatorState newState = Copy(originalState);
                newState.transitions = CopyAll(newState.transitions, t => Copy(t));
                newState.behaviours = CopyAll(newState.behaviours, b => Copy(b));
                return newState;
            }

            T Copy<T>(T originalObject) where T : Object
            {
                T newObject = Object.Instantiate(originalObject);
                newObject.name = originalObject.name;
                objectMap[originalObject] = newObject;
                return newObject;
            }

            T[] CopyAll<T>(T[] originalObjects, Func<T, T> copier)
                => originalObjects.Select(o => copier(o)).ToArray();
        }
    }
}
