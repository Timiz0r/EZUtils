namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using VRC.SDK3.Avatars.Components;
    using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

    public class AvatarPlayableAnimator
    {
        private readonly VRCAvatarDescriptor avatar;
        private readonly PlayableGraph playableGraph;
        private readonly AnimationLayerMixerPlayable playableMixer;
        private readonly VrcDefaultAnimatorControllers defaultControllers = new VrcDefaultAnimatorControllers();
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
        public AvatarPlayableAnimator(VRCAvatarDescriptor avatar)
        {
            playableGraph = PlayableGraph.Create("AvatarPlayableAnimator");
            playableMixer = AnimationLayerMixerPlayable.Create(playableGraph, PlayableLayerCount);
            this.avatar = avatar;

            //TODO: clean this up, and see if we can use sdk to be the authoritative source
            Dictionary<AnimLayerType, CustomAnimLayer> allLayers =
                (avatar.baseAnimationLayers ?? Enumerable.Empty<CustomAnimLayer>())
                    .Concat(avatar.specialAnimationLayers ?? Enumerable.Empty<CustomAnimLayer>())
                    .ToDictionary(l => l.type, l => l);

            //since the order and configuration of each layer is specific, we'll explicitly and clearly define each layer
            CustomAnimLayer layer = GetLayer(AnimLayerType.Base);
            Base = new AvatarPlayableLayer(0, playableMixer, GetMask(null));
            Base.BindAnimatorController(GetAnimatorController());

            layer = GetLayer(AnimLayerType.Additive);
            Additive = new AvatarPlayableLayer(1, playableMixer, GetMask(null));
            Additive.BindAnimatorController(GetAnimatorController());
            Additive.SetAdditive();

            layer = GetLayer(AnimLayerType.Sitting);
            StationSitting = new AvatarPlayableLayer(2, playableMixer, GetMask(null));
            StationSitting.TurnOff();
            Sitting = new AvatarPlayableLayer(3, playableMixer, GetMask(null));
            Sitting.BindAnimatorController(GetAnimatorController());
            Sitting.TurnOff();

            layer = GetLayer(AnimLayerType.TPose);
            TPose = new AvatarPlayableLayer(4, playableMixer, GetMask(VrcAvatarMasks.MuscleOnly));
            TPose.BindAnimatorController(GetAnimatorController());
            TPose.TurnOff();

            layer = GetLayer(AnimLayerType.IKPose);
            IKPose = new AvatarPlayableLayer(5, playableMixer, GetMask(VrcAvatarMasks.MuscleOnly));
            IKPose.BindAnimatorController(GetAnimatorController());
            IKPose.TurnOff();

            layer = GetLayer(AnimLayerType.Gesture);
            Gesture = new AvatarPlayableLayer(6, playableMixer, GetMask(VrcAvatarMasks.HandsOnly));
            Gesture.BindAnimatorController(GetAnimatorController());

            layer = GetLayer(AnimLayerType.Action);
            StationAction = new AvatarPlayableLayer(7, playableMixer, GetMask(null));
            StationAction.TurnOff();
            Action = new AvatarPlayableLayer(8, playableMixer, GetMask(null));
            Action.BindAnimatorController(GetAnimatorController());
            Action.TurnOff();

            layer = GetLayer(AnimLayerType.FX);
            FX = new AvatarPlayableLayer(9, playableMixer, VrcAvatarMasks.Empty);
            FX.BindAnimatorController(GetAnimatorController());

            CustomAnimLayer GetLayer(AnimLayerType layerType)
                => allLayers.TryGetValue(layerType, out CustomAnimLayer l) ? l : new CustomAnimLayer
                {
                    type = layerType,
                    isDefault = true,
                    isEnabled = false,
                    animatorController = null,
                    mask = null,
                };
            AvatarMask GetMask(AvatarMask defaultMask) => layer.isDefault ? defaultMask : layer.mask;
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

        public void Attach()
        {
            AnimationPlayableOutput animationPlayableOutput =
                AnimationPlayableOutput.Create(playableGraph, "AvatarPlayableAnimator", avatar.GetComponent<Animator>());
            animationPlayableOutput.SetSourcePlayable(playableMixer);

            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            playableGraph.Play();
        }

        //theoretically want a detach for the matching attach as well, but no current need for it
        public void Destroy()
        {
            if (playableGraph.IsValid()) playableGraph.Destroy();
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

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
        public sealed class AvatarPlayableLayerAttribute : Attribute
        {
        }
    }
}