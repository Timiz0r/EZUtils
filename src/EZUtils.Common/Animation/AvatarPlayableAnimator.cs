namespace EZUtils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDKBase;
    using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

    //written not as a component to be viable both as a component and an ad-hoc tool
    public class AvatarPlayableAnimator
    {
        private static bool initializedInitializers = false;
        private readonly VrcDefaultAnimatorControllers defaultControllers = new VrcDefaultAnimatorControllers();
        private readonly VRCAvatarDescriptor avatar;
        //the reference component is consumed by layers for finding the associated AvatarPlayableAnimator for a given Animator
        private readonly Reference referenceComponent;
        private readonly PlayableGraph playableGraph;

        private readonly Dictionary<VRC_PlayableLayerControl.BlendableLayer, AvatarPlayableLayer> playableLayerControlLayers =
            new Dictionary<VRC_PlayableLayerControl.BlendableLayer, AvatarPlayableLayer>();
        private readonly Dictionary<VRC_AnimatorLayerControl.BlendableLayer, AvatarPlayableLayer> animatorLayerControlLayers =
            new Dictionary<VRC_AnimatorLayerControl.BlendableLayer, AvatarPlayableLayer>();

        //the private members here are handled internally as special mechanics
        //also a note that we won't support misconfigured avatars with, for instance, multiple fx layers
        public AvatarPlayableLayer Base { get; }
        public AvatarPlayableLayer Additive { get; }
        private readonly AvatarPlayableLayer StationSitting;
        private readonly AvatarPlayableLayer Sitting;
        private readonly AvatarPlayableLayer TPose;
        private readonly AvatarPlayableLayer IKPose;
        public AvatarPlayableLayer Gesture { get; }
        private readonly AvatarPlayableLayer StationAction;
        public AvatarPlayableLayer Action { get; }
        public AvatarPlayableLayer FX { get; }


        //the ctor has more side-effects than a ctor should, so we want the semantics of a static method like this
        //but with the convenience of having readonly fields set in a ctor
        public static AvatarPlayableAnimator Attach(VRCAvatarDescriptor avatar) => new AvatarPlayableAnimator(avatar);

        private AvatarPlayableAnimator(VRCAvatarDescriptor avatar)
        {
            if (!initializedInitializers)
            {
                //a tough decision: do we trust that, if these are non-null, that gesture manager or av3 emulator
                //will take care of it for us?
                //if we had a way to detect when a new AnimationPlayableOutput gets used with an avatar we're managing
                //we could manage this more safely. instead, we'll just trust the user to do the right thing.
                VRC_PlayableLayerControl.Initialize += InitializeNewPlayableLayerControl;
                VRC_AnimatorLayerControl.Initialize += InitializeNewAnimatorLayerControl;
                initializedInitializers = true;
            }

            this.avatar = avatar;
            referenceComponent = avatar.gameObject.AddComponent<Reference>();
            referenceComponent.avatarPlayableAnimator = this;

            playableGraph = PlayableGraph.Create("AvatarPlayableAnimator");
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            AnimationLayerMixerPlayable playableMixer = AnimationLayerMixerPlayable.Create(playableGraph, Enum.GetNames(typeof(PlayableLayerType)).Length);
            AnimationPlayableOutput animationPlayableOutput =
                AnimationPlayableOutput.Create(playableGraph, "AvatarPlayableAnimator", avatar.GetComponent<Animator>());
            animationPlayableOutput.SetSourcePlayable(playableMixer);

            Dictionary<AnimLayerType, CustomAnimLayer> allLayers =
                (avatar.baseAnimationLayers ?? Enumerable.Empty<CustomAnimLayer>())
                    .Concat(avatar.specialAnimationLayers ?? Enumerable.Empty<CustomAnimLayer>())
                    .ToDictionary(l => l.type, l => l);

            //since the order and configuration of each layer is specific, we'll explicitly and clearly define each layer
            Base = CreateLayer(
                PlayableLayerType.Base);

            Additive = CreateLayer(
                PlayableLayerType.Additive);
            Additive.SetAdditive();

            StationSitting = CreateLayer(
                PlayableLayerType.StationSitting, offByDefault: true, bindAnimatorController: false);

            Sitting = CreateLayer(
                PlayableLayerType.Sitting, offByDefault: true);

            TPose = CreateLayer(
                PlayableLayerType.TPose, maskIfDefault: VrcAvatarMasks.MuscleOnly, maskIfNone: null, offByDefault: true);

            IKPose = CreateLayer(
                PlayableLayerType.IKPose, maskIfDefault: VrcAvatarMasks.MuscleOnly, maskIfNone: null, offByDefault: true);

            Gesture = CreateLayer(
                PlayableLayerType.Gesture, maskIfDefault: VrcAvatarMasks.HandsOnly, maskIfNone: null);

            StationAction = CreateLayer(
                PlayableLayerType.StationAction, offByDefault: true, bindAnimatorController: false);

            Action = CreateLayer(
                PlayableLayerType.Action, offByDefault: true);

            FX = CreateLayer(
                PlayableLayerType.FX, maskIfDefault: VrcAvatarMasks.NoHumanoid, maskIfNone: VrcAvatarMasks.NoHumanoid);

            AvatarPlayableLayer CreateLayer(
                PlayableLayerType layer,
                AvatarMask maskIfDefault = null,
                AvatarMask maskIfNone = null,
                bool bindAnimatorController = true,
                bool offByDefault = false)
            {
                PlayableLayerMappingAttribute mapping = typeof(PlayableLayerType)
                    .GetField(Enum.GetName(typeof(PlayableLayerType), layer))
                    .GetCustomAttribute<PlayableLayerMappingAttribute>();
                CustomAnimLayer avatarLayer = allLayers.TryGetValue(mapping.LayerType, out CustomAnimLayer l) ? l : new CustomAnimLayer
                {
                    type = mapping.LayerType,
                    isDefault = true,
                    animatorController = null,
                    mask = null,
                };
                AvatarMask mask = avatarLayer.isDefault
                    ? maskIfDefault
                    : avatarLayer.mask == null
                        ? maskIfNone
                        : avatarLayer.mask;
                AvatarPlayableLayer playableLayer = new AvatarPlayableLayer(layer, playableMixer, mask);

                if (bindAnimatorController)
                {
                    RuntimeAnimatorController controller = GetAnimatorController(avatarLayer);
                    playableLayer.BindAnimatorController(controller);
                }

                if (offByDefault)
                {
                    playableLayer.TurnOff();
                }

                if (mapping.BlendablePlayableLayer != null)
                {
                    playableLayerControlLayers[mapping.BlendablePlayableLayer.Value] = playableLayer;
                }
                if (mapping.BlendableAnimatorLayer != null)
                {
                    animatorLayerControlLayers[mapping.BlendableAnimatorLayer.Value] = playableLayer;
                }

                return playableLayer;
            }
        }

        public void Detach()
        {
            Stop();
            if (playableGraph.IsValid()) playableGraph.Destroy();
            UnityEngine.Object.Destroy(referenceComponent);
        }

        public void Start()
        {
            if (playableGraph.IsValid() && !playableGraph.IsPlaying())
            {
                playableGraph.Play();
            }
        }

        public void Stop()
        {
            if (playableGraph.IsValid() && playableGraph.IsPlaying())
            {
                playableGraph.Stop();
            }
            avatar.GetComponent<Animator>().Rebind();
        }

        public void EnterStationSitting(RuntimeAnimatorController animatorController)
        {
            Sitting.TurnOn();
            StationSitting.BindAnimatorController(animatorController);
            StationAction.UnbindAnimator();
        }

        public void EnterStationNonSitting(RuntimeAnimatorController animatorController)
        {
            Sitting.TurnOff();
            StationSitting.UnbindAnimator();
            StationAction.BindAnimatorController(animatorController);
        }

        public void ExitStation()
        {
            Sitting.TurnOff();
            StationSitting.UnbindAnimator();
            StationAction.UnbindAnimator();
        }

        public void StartTPose() => TPose.TurnOn();
        public void StopTPose() => TPose.TurnOff();
        public void StartIKPose() => IKPose.TurnOn();
        public void StopIKPose() => IKPose.TurnOff();

        private static void InitializeNewAnimatorLayerControl(VRC_AnimatorLayerControl obj)
            => obj.ApplySettings += EvaluateAnimatorLayerControl;
        private static void InitializeNewPlayableLayerControl(VRC_PlayableLayerControl obj)
            => obj.ApplySettings += EvaluatePlayableLayerControl;

        private static void EvaluatePlayableLayerControl(VRC_PlayableLayerControl control, Animator animator)
        {
            if (!animator.TryGetComponent(out Reference reference)) return;

            AvatarPlayableLayer layer = reference.avatarPlayableAnimator.playableLayerControlLayers[control.layer];
            layer.SetGoalWeight(control.goalWeight, control.blendDuration);
        }
        private static void EvaluateAnimatorLayerControl(VRC_AnimatorLayerControl control, Animator animator)
        {
            if (!animator.TryGetComponent(out Reference reference)) return;

            AvatarPlayableLayer layer = reference.avatarPlayableAnimator.animatorLayerControlLayers[control.playable];
            layer.SetLayerGoalWeight(control.layer, control.goalWeight, control.blendDuration);
        }

        private RuntimeAnimatorController GetAnimatorController(CustomAnimLayer layer)
        {
            if (!layer.isDefault && layer.animatorController != null) return layer.animatorController;
            switch (layer.type)
            {
                case AnimLayerType.Base:
                    return defaultControllers.Base;
                case AnimLayerType.Additive:
                    return defaultControllers.Additive;
                case AnimLayerType.Gesture:
                    return defaultControllers.Gesture;
                case AnimLayerType.Action:
                    return defaultControllers.Action;
                case AnimLayerType.FX:
                    return defaultControllers.FX;
                case AnimLayerType.Sitting:
                    return defaultControllers.Sitting;
                case AnimLayerType.TPose:
                    return defaultControllers.TPose;
                case AnimLayerType.IKPose:
                    return defaultControllers.IKPose;
                case AnimLayerType.Deprecated0:
                    throw new InvalidOperationException($"No support for layer '{nameof(AnimLayerType.Deprecated0)}'.");
                default:
                    throw new InvalidOperationException($"Unknown layer '{layer.type}' found.");
            }
        }

        //avoiding making AvatarPlayableAnimator itself a component, since component conventions are really annoying
        //but it's certainly an option
        private class Reference : MonoBehaviour
        {
            public AvatarPlayableAnimator avatarPlayableAnimator;

            public void Start() => hideFlags = HideFlags.HideInInspector;
        }
    }

}
