namespace EZUtils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;
    using Object = UnityEngine.Object;

    //is not static to allow workflows where these loaded assets are then modified.
    //would not one consumer to affect another consumer.
    //also worth noting making them static and non-lazy doesnt work in unit tests for some reason
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

        //could also try to create a more generic version using SO more. the hard part is know what to copy.
        //for instance, we dont copy motions or avatarmasks, but they would certainly be object references.
        //maybe checking assetdatabase to see if it's a subasset would work.
        //not opposed to reflection, but, as long as we do a validation pass when upgrading sdk, this kinda has
        //better peace-of-mind.
        private static AnimatorController DeepCopy(AnimatorController original)
        {
            Dictionary<Object, Object> objectMap = new Dictionary<Object, Object>();
            HashSet<Object> traversedObjects = new HashSet<Object>();

            AnimatorController newAnimatorController = CopyByReflection(original);
            newAnimatorController.layers = CopyAll(original.layers, l =>
            {
                AnimatorControllerLayer newLayer = CopyByReflection(l);
                newLayer.stateMachine = CopyStateMachine(l.stateMachine);
                return newLayer;
            });

            SwapReferences(newAnimatorController);

            return newAnimatorController;

            void SwapReferences(Object target)
            {
                if (traversedObjects.Contains(target)) return;
                _ = traversedObjects.Add(target);

                using (SerializedObject so = new SerializedObject(target))
                {
                    SerializedProperty targetIterator = so.GetIterator();
                    while (targetIterator.Next(enterChildren: true))
                    {
                        if (targetIterator.propertyType != SerializedPropertyType.ObjectReference
                            || targetIterator.objectReferenceValue == null) continue;

                        if (objectMap.TryGetValue(targetIterator.objectReferenceValue, out Object newObject))
                        {
                            targetIterator.objectReferenceValue = newObject;
                        }
                        //which has potentially changed based on the above swap
                        //or mayb have already been changed prior, like when we set the new state machines for the new layers
                        SwapReferences(targetIterator.objectReferenceValue);
                    }
                    _ = so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            AnimatorStateMachine CopyStateMachine(AnimatorStateMachine originalStateMachine)
            {
                AnimatorStateMachine newStateMachine = CopyByReflection(originalStateMachine);
                newStateMachine.anyStateTransitions =
                    CopyAll(originalStateMachine.anyStateTransitions, t => CopyByReflection(t));
                newStateMachine.entryTransitions =
                    CopyAll(originalStateMachine.entryTransitions, t => CopyByReflection(t));
                newStateMachine.states = originalStateMachine.states
                    .Select(cas => new ChildAnimatorState()
                    {
                        position = cas.position,
                        state = CopyState(cas.state)
                    })
                    .ToArray();
                newStateMachine.stateMachines = originalStateMachine.stateMachines
                    .Select(casm => new ChildAnimatorStateMachine()
                    {
                        position = casm.position,
                        stateMachine = CopyStateMachine(casm.stateMachine)
                    })
                    .ToArray();
                newStateMachine.behaviours = CopyAll(originalStateMachine.behaviours, b => CopyBehaviour(b));
                //the copy by reflection causes the default state to be the first state in the state array
                //so we set it back to the mapped version of the original default state
                if (newStateMachine.defaultState != null)
                {
                    newStateMachine.defaultState = (AnimatorState)objectMap[originalStateMachine.defaultState];
                }

                return newStateMachine;
            }

            AnimatorState CopyState(AnimatorState originalState)
            {
                AnimatorState newState = CopyByReflection(originalState);
                newState.behaviours = CopyAll(originalState.behaviours, b => CopyBehaviour(b));
                newState.transitions = CopyAll(originalState.transitions, t => CopyByReflection(t));

                return newState;
            }

            StateMachineBehaviour CopyBehaviour(StateMachineBehaviour originalBehaviour)
            {
                StateMachineBehaviour newBehaviour = Object.Instantiate(originalBehaviour);
                newBehaviour.name = originalBehaviour.name;
                objectMap[originalBehaviour] = newBehaviour;
                return newBehaviour;
            }

            T[] CopyAll<T>(T[] originalObjects, Func<T, T> copier)
                => originalObjects.Select(o => copier(o)).ToArray();

            T CopyByReflection<T>(T originalObject) where T : new()
            {
                T newObject = new T();
                foreach (PropertyInfo property in typeof(T)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.CanWrite))
                {
                    property.SetValue(newObject, property.GetValue(originalObject));
                }
                if (originalObject is Object originalObjectObject)
                {
                    objectMap[originalObjectObject] = newObject as Object;
                }
                return newObject;
            }
        }
    }
}
