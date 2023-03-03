namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor.Animations;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;
    using AnimLayerType = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType;

    public class PlayableLayerInformation
    {
        public PlayableLayer Base { get; }
        public PlayableLayer Additive { get; }
        public PlayableLayer Gesture { get; }
        public PlayableLayer Action { get; }
        public PlayableLayer FX { get; }
        public PlayableLayer Sitting { get; }
        public PlayableLayer TPose { get; }
        public PlayableLayer IKPose { get; }

        public PlayableLayerInformation(
            PlayableLayer @base,
            PlayableLayer additive,
            PlayableLayer gesture,
            PlayableLayer action,
            PlayableLayer fx,
            PlayableLayer sitting,
            PlayableLayer tPose,
            PlayableLayer ikPose)
        {
            Base = @base;
            Additive = additive;
            Gesture = gesture;
            Action = action;
            FX = fx;
            Sitting = sitting;
            TPose = tPose;
            IKPose = ikPose;
        }

        public static PlayableLayerInformation Generate(VRCAvatarDescriptor avatar)
        {
            AnimatorStateMachine[] allStateMachines = avatar.baseAnimationLayers
                .Where(l => !l.isDefault && l.animatorController != null)
                .Select(l => (AnimatorController)l.animatorController)
                .SelectMany(c => c.layers.Select(l => l.stateMachine))
                .ToArray();

            HashSet<AnimLayerType> possiblyDisabledPlayableLayers = new HashSet<AnimLayerType>(allStateMachines
                .SelectMany(sm => GetBehaviours<VRCPlayableLayerControl>(sm))
                .Where(b => b.goalWeight == 0)
                .Select(b => ConvertEnum(b.layer)));
            HashSet<(AnimLayerType, int)> possiblyDisabledAnimatorLayers = new HashSet<(AnimLayerType, int)>(allStateMachines
                .SelectMany(sm => GetBehaviours<VRCAnimatorLayerControl>(sm))
                .Where(b => b.goalWeight == 0)
                .Select(b => (ConvertEnum(b.playable), b.layer))
                .Distinct());

            PlayableLayerInformation result = new PlayableLayerInformation(
                @base: GenerateLayer(AnimLayerType.Base),
                additive: GenerateLayer(AnimLayerType.Additive),
                action: GenerateLayer(AnimLayerType.Action),
                gesture: GenerateLayer(AnimLayerType.Gesture),
                fx: GenerateLayer(AnimLayerType.FX),
                sitting: GenerateLayer(AnimLayerType.Sitting),
                tPose: GenerateLayer(AnimLayerType.TPose),
                ikPose: GenerateLayer(AnimLayerType.IKPose)
            );
            return result;

            PlayableLayer GenerateLayer(AnimLayerType layerType)
            {
                VRCAvatarDescriptor.CustomAnimLayer layer = avatar.baseAnimationLayers.SingleOrDefault(
                    l => !l.isDefault && l.type == layerType);
                if (layer.animatorController == null) return new PlayableLayer(
                    states: Array.Empty<State>(),
                    configuredMask: null,
                    underlyingController: null);

                bool playableLayerPossiblyDisabled = possiblyDisabledPlayableLayers.Contains(layerType);
                AnimatorController controller = (AnimatorController)layer.animatorController;
                //dont know if layers can be null, but since a controller can have no layers, do this just in case
                State[] states = controller.layers
                    ?.SelectMany((l, layerIndex) => l.stateMachine.states
                        .Select(s => new State(
                            layerIndex: layerIndex,
                            layerName: l.name,
                            stateName: s.state.name,
                            isAlwaysDisabled:
                                l.defaultWeight == 0 && !possiblyDisabledAnimatorLayers.Contains((layerType, layerIndex)),
                            mayGetDisabledByBehaviour:
                                playableLayerPossiblyDisabled
                                || possiblyDisabledAnimatorLayers.Contains((layerType, layerIndex)),
                            underlyingState: s.state)))
                    ?.ToArray() ?? Array.Empty<State>();
                return new PlayableLayer(
                    states,
                    //as opposed to looking at Cust
                    layer.mask,
                    controller);
            }
        }

        private static IEnumerable<T> GetBehaviours<T>(AnimatorStateMachine sm) where T : StateMachineBehaviour
            => sm.behaviours
                .Concat(sm.states.SelectMany(s => s.state.behaviours))
                .Concat(sm.stateMachines
                    .Select(sm2 => sm2.stateMachine)
                    .SelectMany(sm2 => GetBehaviours<T>(sm2)))
                .OfType<T>();

        private static AnimLayerType ConvertEnum<T>(T original) where T : Enum
            => Enum.TryParse(
                    Enum.GetName(original.GetType(), original),
                    out AnimLayerType l)
                        ? l
                        : throw new InvalidOperationException();

        public class PlayableLayer
        {
            public IReadOnlyList<State> States { get; }
            public AvatarMask ConfiguredMask { get; }
            public AnimatorController UnderlyingController { get; }

            public PlayableLayer(
                IReadOnlyList<State> states, AvatarMask configuredMask, AnimatorController underlyingController)
            {
                States = states;
                ConfiguredMask = configuredMask;
                UnderlyingController = underlyingController;
            }
        }

        public class State
        {
            public int LayerIndex { get; }
            public string LayerName { get; }
            public string StateName { get; }
            public bool IsAlwaysDisabled { get; }
            public bool MayGetDisabledByBehaviour { get; }
            public AnimatorState UnderlyingState { get; }

            public State(int layerIndex, string layerName, string stateName, bool isAlwaysDisabled, bool mayGetDisabledByBehaviour, AnimatorState underlyingState)
            {
                LayerIndex = layerIndex;
                LayerName = layerName;
                StateName = stateName;
                IsAlwaysDisabled = isAlwaysDisabled;
                MayGetDisabledByBehaviour = mayGetDisabledByBehaviour;
                UnderlyingState = underlyingState;
            }
        }
    }
}
