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

    public class AvatarPlayableAnimator
    {
        private static bool initializedInitializers = false;
        private readonly VRCAvatarDescriptor avatar;
        private readonly Reference referenceComponent;

        private readonly PlayableGraph playableGraph;
        private readonly AnimationLayerMixerPlayable playableMixer;
        private readonly AnimationPlayableOutput animationPlayableOutput;
        private readonly VrcDefaultAnimatorControllers defaultControllers = new VrcDefaultAnimatorControllers();

        private readonly Dictionary<VRC_PlayableLayerControl.BlendableLayer, AvatarPlayableLayer> playableLayerControlLayers =
            new Dictionary<VRC_PlayableLayerControl.BlendableLayer, AvatarPlayableLayer>();
        private readonly Dictionary<VRC_AnimatorLayerControl.BlendableLayer, AvatarPlayableLayer> animatorLayerControlLayers =
            new Dictionary<VRC_AnimatorLayerControl.BlendableLayer, AvatarPlayableLayer>();

        private static readonly int PlayableLayerCount = typeof(AvatarPlayableAnimator)
            .GetMembers(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .Count(m => m.GetCustomAttribute<AvatarPlayableLayerAttribute>() != null);
        //the private members here are handled internally as special mechanics
        //also a note that we won't support misconfigured avatars with, for instance, multiple fx layers
        [AvatarPlayableLayer] public AvatarPlayableLayer Base { get; }
        [AvatarPlayableLayer] public AvatarPlayableLayer Additive { get; }
        [AvatarPlayableLayer] private readonly AvatarPlayableLayer StationSitting;
        [AvatarPlayableLayer] private readonly AvatarPlayableLayer Sitting;
        [AvatarPlayableLayer] private readonly AvatarPlayableLayer TPose;
        [AvatarPlayableLayer] private readonly AvatarPlayableLayer IKPose;
        [AvatarPlayableLayer] public AvatarPlayableLayer Gesture { get; }
        [AvatarPlayableLayer] private readonly AvatarPlayableLayer StationAction;

        [AvatarPlayableLayer] public AvatarPlayableLayer Action { get; }
        [AvatarPlayableLayer] public AvatarPlayableLayer FX { get; }


        //written not as a component to be viable both as a component and an ad-hoc tool
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
            playableMixer = AnimationLayerMixerPlayable.Create(playableGraph, PlayableLayerCount);
            animationPlayableOutput =
                AnimationPlayableOutput.Create(playableGraph, "AvatarPlayableAnimator", avatar.GetComponent<Animator>());
            animationPlayableOutput.SetSourcePlayable(playableMixer);

            Dictionary<AnimLayerType, CustomAnimLayer> allLayers =
                (avatar.baseAnimationLayers ?? Enumerable.Empty<CustomAnimLayer>())
                    .Concat(avatar.specialAnimationLayers ?? Enumerable.Empty<CustomAnimLayer>())
                    .ToDictionary(l => l.type, l => l);

            //since the order and configuration of each layer is specific, we'll explicitly and clearly define each layer
            CustomAnimLayer layer = GetLayer(AnimLayerType.Base);
            Base = new AvatarPlayableLayer(0, playableMixer, GetMask(ifDefault: null, ifNone: null));
            Base.BindAnimatorController(GetAnimatorController());

            layer = GetLayer(AnimLayerType.Additive);
            Additive = new AvatarPlayableLayer(1, playableMixer, GetMask(ifDefault: null, ifNone: null));
            Additive.BindAnimatorController(GetAnimatorController());
            Additive.SetAdditive();

            layer = GetLayer(AnimLayerType.Sitting);
            StationSitting = new AvatarPlayableLayer(2, playableMixer, GetMask(ifDefault: null, ifNone: null));
            StationSitting.TurnOff();
            Sitting = new AvatarPlayableLayer(3, playableMixer, GetMask(ifDefault: null, ifNone: null));
            Sitting.BindAnimatorController(GetAnimatorController());
            Sitting.TurnOff();

            layer = GetLayer(AnimLayerType.TPose);
            TPose = new AvatarPlayableLayer(4, playableMixer, GetMask(ifDefault: VrcAvatarMasks.MuscleOnly, ifNone: null));
            TPose.BindAnimatorController(GetAnimatorController());
            TPose.TurnOff();

            layer = GetLayer(AnimLayerType.IKPose);
            IKPose = new AvatarPlayableLayer(5, playableMixer, GetMask(ifDefault: VrcAvatarMasks.MuscleOnly, ifNone: null));
            IKPose.BindAnimatorController(GetAnimatorController());
            IKPose.TurnOff();

            layer = GetLayer(AnimLayerType.Gesture);
            Gesture = new AvatarPlayableLayer(6, playableMixer, GetMask(ifDefault: VrcAvatarMasks.HandsOnly, ifNone: null));
            Gesture.BindAnimatorController(GetAnimatorController());

            layer = GetLayer(AnimLayerType.Action);
            StationAction = new AvatarPlayableLayer(7, playableMixer, GetMask(ifDefault: null, ifNone: null));
            StationAction.TurnOff();
            Action = new AvatarPlayableLayer(8, playableMixer, GetMask(ifDefault: null, ifNone: null));
            Action.BindAnimatorController(GetAnimatorController());
            Action.TurnOff();

            layer = GetLayer(AnimLayerType.FX);
            FX = new AvatarPlayableLayer(9, playableMixer, GetMask(ifDefault: VrcAvatarMasks.NoHumanoid, ifNone: VrcAvatarMasks.NoHumanoid));
            FX.BindAnimatorController(GetAnimatorController());

            playableLayerControlLayers[VRC_PlayableLayerControl.BlendableLayer.Additive] = Additive;
            playableLayerControlLayers[VRC_PlayableLayerControl.BlendableLayer.Action] = Action;
            playableLayerControlLayers[VRC_PlayableLayerControl.BlendableLayer.Gesture] = Gesture;
            playableLayerControlLayers[VRC_PlayableLayerControl.BlendableLayer.FX] = FX;
            animatorLayerControlLayers[VRC_AnimatorLayerControl.BlendableLayer.Additive] = Additive;
            animatorLayerControlLayers[VRC_AnimatorLayerControl.BlendableLayer.Action] = Action;
            animatorLayerControlLayers[VRC_AnimatorLayerControl.BlendableLayer.Gesture] = Gesture;
            animatorLayerControlLayers[VRC_AnimatorLayerControl.BlendableLayer.FX] = FX;

            CustomAnimLayer GetLayer(AnimLayerType layerType)
                => allLayers.TryGetValue(layerType, out CustomAnimLayer l) ? l : new CustomAnimLayer
                {
                    type = layerType,
                    isDefault = true,
                    isEnabled = false,
                    animatorController = null,
                    mask = null,
                };
            AvatarMask GetMask(AvatarMask ifDefault, AvatarMask ifNone)
                => layer.isDefault
                    ? ifDefault
                    : layer.mask == null
                        ? ifNone
                        : layer.mask;
            RuntimeAnimatorController GetAnimatorController()
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
        }

        //the ctor has more side-effects than a ctor should, so we want the semantics of a static method like this
        //but with the convenience of having readonly fields set in a ctor
        public static AvatarPlayableAnimator Attach(VRCAvatarDescriptor avatar) => new AvatarPlayableAnimator(avatar);

        public void Detach()
        {
            Stop();
            if (playableGraph.IsValid()) playableGraph.Destroy();
            UnityEngine.Object.DestroyImmediate(referenceComponent);
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

        private static void InitializeNewAnimatorLayerControl(VRC_AnimatorLayerControl obj)
            => obj.ApplySettings += EvaluateAnimatorLayerControl;
        private static void InitializeNewPlayableLayerControl(VRC_PlayableLayerControl obj)
            => obj.ApplySettings += EvaluatePlayableLayerControl;

        private static void EvaluatePlayableLayerControl(VRC_PlayableLayerControl control, Animator animator)
        {
            Reference reference = animator.GetComponent<Reference>();
            if (reference == null) return;

            AvatarPlayableLayer layer = reference.avatarPlayableAnimator.playableLayerControlLayers[control.layer];
            layer.SetGoalWeight(control.goalWeight, control.blendDuration);
        }
        private static void EvaluateAnimatorLayerControl(VRC_AnimatorLayerControl control, Animator animator)
        {
            Reference reference = animator.GetComponent<Reference>();
            if (reference == null) return;

            AvatarPlayableLayer layer = reference.avatarPlayableAnimator.animatorLayerControlLayers[control.playable];
            layer.SetLayerGoalWeight(control.layer, control.goalWeight, control.blendDuration);
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
        private sealed class AvatarPlayableLayerAttribute : Attribute
        {
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