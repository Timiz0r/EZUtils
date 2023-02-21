namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;
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

        //could also try to create a more generic version using SO more. the hard part is know what to copy.
        //for instance, we dont copy motions or avatarmasks, but they would certainly be object references.
        //maybe checking assetdatabase to see if it's a subasset would work.
        //not opposed to reflection, but, as long as we do a validation pass when upgrading sdk, this kinda has
        //better peace-of-mind.
        private static AnimatorController DeepCopy(AnimatorController original)
        {
            Dictionary<Object, Object> objectMap = new Dictionary<Object, Object>();
            HashSet<Object> traversedObjects = new HashSet<Object>();
            AnimatorController copy = CopyAnimatorController();

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
            }

            return copy;

            AnimatorController CopyAnimatorController()
            {
                AnimatorController newAnimatorController = new AnimatorController()
                {
                    hideFlags = original.hideFlags,
                    name = original.name,
                    parameters = CopyAll(original.parameters, p => new AnimatorControllerParameter()
                    {
                        defaultBool = p.defaultBool,
                        defaultFloat = p.defaultFloat,
                        defaultInt = p.defaultInt,
                        name = p.name,
                        type = p.type,
                    }),
                    layers = CopyAll(original.layers, l => new AnimatorControllerLayer()
                    {
                        avatarMask = l.avatarMask,
                        name = l.name,
                        blendingMode = l.blendingMode,
                        defaultWeight = l.defaultWeight,
                        iKPass = l.iKPass,
                        stateMachine = CopyStateMachine(l.stateMachine),
                        syncedLayerAffectsTiming = l.syncedLayerAffectsTiming,
                        syncedLayerIndex = l.syncedLayerIndex,
                    })
                };

                objectMap[original] = newAnimatorController;
                return newAnimatorController;
            }

            AnimatorStateMachine CopyStateMachine(AnimatorStateMachine originalStateMachine)
            {
                //cannot object.instantiate AnimatorStateMachine due to an assertion of unknown cause 🤷‍
                AnimatorStateMachine newStateMachine = new AnimatorStateMachine()
                {
                    anyStatePosition = originalStateMachine.anyStatePosition,
                    name = originalStateMachine.name,
                    anyStateTransitions = CopyAll(originalStateMachine.anyStateTransitions, t => CopyAnimatorStateTransition(t)),
                    entryTransitions = CopyAll(originalStateMachine.entryTransitions, t => CopyAnimatorTransition(t)),
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
                    behaviours = CopyAll(originalStateMachine.behaviours, b => CopyBehaviour(b)),
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
                AnimatorState newState = new AnimatorState()
                {
                    behaviours = CopyAll(originalState.behaviours, b => CopyBehaviour(b)),
                    transitions = CopyAll(originalState.transitions, t => CopyAnimatorStateTransition(t)),
                    cycleOffset = originalState.cycleOffset,
                    cycleOffsetParameter = originalState.cycleOffsetParameter,
                    cycleOffsetParameterActive = originalState.cycleOffsetParameterActive,
                    hideFlags = originalState.hideFlags,
                    iKOnFeet = originalState.iKOnFeet,
                    mirror = originalState.mirror,
                    mirrorParameter = originalState.mirrorParameter,
                    mirrorParameterActive = originalState.mirrorParameterActive,
                    motion = originalState.motion,
                    name = originalState.name,
                    speed = originalState.speed,
                    speedParameter = originalState.speedParameter,
                    speedParameterActive = originalState.speedParameterActive,
                    tag = originalState.tag,
                    timeParameter = originalState.timeParameter,
                    timeParameterActive = originalState.timeParameterActive,
                    writeDefaultValues = originalState.writeDefaultValues,
                };

                objectMap[originalState] = newState;
                return newState;
            }

            AnimatorStateTransition CopyAnimatorStateTransition(AnimatorStateTransition originalTransition)
            {
                AnimatorStateTransition newTransition = new AnimatorStateTransition()
                {
                    canTransitionToSelf = originalTransition.canTransitionToSelf,
                    duration = originalTransition.duration,
                    exitTime = originalTransition.exitTime,
                    hasExitTime = originalTransition.hasExitTime,
                    hasFixedDuration = originalTransition.hasFixedDuration,
                    interruptionSource = originalTransition.interruptionSource,
                    offset = originalTransition.offset,
                    orderedInterruption = originalTransition.orderedInterruption,
                };
                CopyBaseTransitionMembers(originalTransition, newTransition);

                objectMap[originalTransition] = newTransition;
                return newTransition;
            }

            AnimatorTransition CopyAnimatorTransition(AnimatorTransition originalTransition)
            {
                //even though these assignments are duplicated in CopyBaseTransitionMembers,
                //we have them here as well to make it easier to verify we got them all
                AnimatorTransition newTransition = new AnimatorTransition()
                {
                    destinationState = originalTransition.destinationState,
                    destinationStateMachine = originalTransition.destinationStateMachine,
                    name = originalTransition.name,
                    conditions = originalTransition.conditions,
                    hideFlags = originalTransition.hideFlags,
                    isExit = originalTransition.isExit,
                    mute = originalTransition.mute,
                    solo = originalTransition.solo,
                };
                CopyBaseTransitionMembers(originalTransition, newTransition);

                objectMap[originalTransition] = newTransition;
                return newTransition;
            }

            void CopyBaseTransitionMembers(
                AnimatorTransitionBase originalTransition, AnimatorTransitionBase newTransition)
            {
                //dont copy states or state machines, since we do that as part of this transition's state machine
                newTransition.destinationState = originalTransition.destinationState;
                newTransition.destinationStateMachine = originalTransition.destinationStateMachine;
                newTransition.name = originalTransition.name;
                newTransition.conditions = originalTransition.conditions;
                newTransition.hideFlags = originalTransition.hideFlags;
                newTransition.isExit = originalTransition.isExit;
                newTransition.mute = originalTransition.mute;
                newTransition.solo = originalTransition.solo;
            }

            StateMachineBehaviour CopyBehaviour(StateMachineBehaviour originalObject)
            {
                StateMachineBehaviour newObject = Object.Instantiate(originalObject);
                newObject.name = originalObject.name;
                return newObject;
            }
            T[] CopyAll<T>(T[] originalObjects, Func<T, T> copier)
                => originalObjects.Select(o => copier(o)).ToArray();
        }
    }
}
