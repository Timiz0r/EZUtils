namespace EZUtils.MMDAvatarTools
{
    using System;
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
            playableGraph = PlayableGraph.Create("MMDAvatarTester");
            playableMixer = AnimationLayerMixerPlayable.Create(playableGraph, PlayableLayerCount);
            this.avatar = avatar;

            //TODO: clean this up, and see if we can use sdk to be the authoritative source
            CustomAnimLayer[] baseLayers = avatar.baseAnimationLayers ?? new[]
            {
                new CustomAnimLayer()
                {
                    isDefault = true,
                    isEnabled = false,
                    mask = null,
                    type = AnimLayerType.Base
                },
                new CustomAnimLayer()
                {
                    isDefault = true,
                    isEnabled = false,
                    mask = null,
                    type = AnimLayerType.Additive
                },
                new CustomAnimLayer()
                {
                    isDefault = true,
                    isEnabled = false,
                    mask = null,
                    type = AnimLayerType.Gesture
                },
                new CustomAnimLayer()
                {
                    isDefault = true,
                    isEnabled = false,
                    mask = null,
                    type = AnimLayerType.Action
                },
                new CustomAnimLayer()
                {
                    isDefault = true,
                    isEnabled = false,
                    mask = null,
                    type = AnimLayerType.FX
                },
            };
            CustomAnimLayer[] specialLayers = avatar.specialAnimationLayers ?? new[]
            {
                new CustomAnimLayer()
                {
                    isDefault = true,
                    isEnabled = false,
                    mask = null,
                    type = AnimLayerType.Sitting
                },
                new CustomAnimLayer()
                {
                    isDefault = true,
                    isEnabled = false,
                    mask = null,
                    type = AnimLayerType.TPose
                },
                new CustomAnimLayer()
                {
                    isDefault = true,
                    isEnabled = false,
                    mask = null,
                    type = AnimLayerType.IKPose
                },
            };

            foreach (CustomAnimLayer layer in baseLayers.Concat(specialLayers))
            {
                switch (layer.type)
                {
                    case VRCAvatarDescriptor.AnimLayerType.Base:
                        //a note that the first symbol of each line is for the appropriate error
                        //making it easier to notice errors
                        Base = new AvatarPlayableLayer(0, playableMixer, GetMask(null));
                        Base.BindAnimatorController(GetAnimatorController());
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Additive:
                        Additive = new AvatarPlayableLayer(1, playableMixer, GetMask(null));
                        Additive.BindAnimatorController(GetAnimatorController());
                        Additive.SetAdditive();
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Sitting:
                        StationSitting = new AvatarPlayableLayer(2, playableMixer, GetMask(null));
                        StationSitting.TurnOff();

                        Sitting = new AvatarPlayableLayer(3, playableMixer, GetMask(null));
                        Sitting.BindAnimatorController(GetAnimatorController());
                        Sitting.TurnOff();
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.TPose:
                        TPose = new AvatarPlayableLayer(4, playableMixer, GetMask(VrcAvatarMasks.MuscleOnly));
                        TPose.BindAnimatorController(GetAnimatorController());
                        TPose.TurnOff();
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.IKPose:
                        IKPose = new AvatarPlayableLayer(5, playableMixer, GetMask(VrcAvatarMasks.MuscleOnly));
                        IKPose.BindAnimatorController(GetAnimatorController());
                        IKPose.TurnOff();
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Gesture:
                        Gesture = new AvatarPlayableLayer(6, playableMixer, GetMask(VrcAvatarMasks.HandsOnly));
                        Gesture.BindAnimatorController(GetAnimatorController());
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Action:
                        StationAction = new AvatarPlayableLayer(7, playableMixer, GetMask(null));
                        StationAction.TurnOff();

                        Action = new AvatarPlayableLayer(8, playableMixer, GetMask(null));
                        Action.BindAnimatorController(GetAnimatorController());
                        Action.TurnOff();
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.FX:
                        FX = new AvatarPlayableLayer(9, playableMixer, VrcAvatarMasks.Empty);
                        FX.BindAnimatorController(GetAnimatorController());
                        break;
                    case VRCAvatarDescriptor.AnimLayerType.Deprecated0:
                        throw new InvalidOperationException($"No support for layer '{nameof(AnimLayerType.Deprecated0)}'.");
                    default:
                        throw new InvalidOperationException($"Unknown layer '{layer.type}' found.");
                }
                AvatarMask GetMask(AvatarMask defaultMask) => layer.isDefault ? defaultMask : layer.mask;
                RuntimeAnimatorController GetAnimatorController()
                {
                    if (!layer.isDefault && layer.animatorController != null) return layer.animatorController;
                    switch (layer.type)
                    {
                        case AnimLayerType.Base:
                            return VrcDefaultAnimatorControllers.Base;
                        case AnimLayerType.Additive:
                            return VrcDefaultAnimatorControllers.Additive;
                        case AnimLayerType.Gesture:
                            return VrcDefaultAnimatorControllers.Gesture;
                        case AnimLayerType.Action:
                            return VrcDefaultAnimatorControllers.Action;
                        case AnimLayerType.FX:
                            return VrcDefaultAnimatorControllers.FX;
                        case AnimLayerType.Sitting:
                            return VrcDefaultAnimatorControllers.Sitting;
                        case AnimLayerType.TPose:
                            return VrcDefaultAnimatorControllers.TPose;
                        case AnimLayerType.IKPose:
                            return VrcDefaultAnimatorControllers.IKPose;
                        case AnimLayerType.Deprecated0:
                            throw new InvalidOperationException($"No support for layer '{nameof(AnimLayerType.Deprecated0)}'.");
                        default:
                            throw new InvalidOperationException($"Unknown layer '{layer.type}' found.");
                    }
                }
            }
        }

        public void Attach()
        {
            AnimationPlayableOutput animationPlayableOutput =
                AnimationPlayableOutput.Create(playableGraph, "MMDAvatarTester", avatar.GetComponent<Animator>());
            animationPlayableOutput.SetSourcePlayable(playableMixer);

            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            playableGraph.Play();
            playableGraph.Evaluate(0f);
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
