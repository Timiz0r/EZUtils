namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using UnityEngine.UIElements;
    using VRC.SDK3.Avatars.Components;
    using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;

    public class MMDAvatarTesterEditorWindow : EditorWindow
    {
        private bool validAvatarIsTargeted = false;
        private bool animatorControllerIsTargeted = false;
        private AvatarPlayableAnimator avatarPlayableAnimator = null;

        [MenuItem("EZUtils/MMD avatar tester", isValidateFunction: false, priority: 0)]
        public static void PackageManager()
        {
            MMDAvatarTesterEditorWindow window = GetWindow<MMDAvatarTesterEditorWindow>("MMD Tester");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.EZUtils.MMDAvatarTools/MMDAvatarTesterEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            //probably dont need allowSceneObjects, but meh
            ObjectField targetAvatar = rootVisualElement.Q<ObjectField>(name: "targetAvatar");
            targetAvatar.objectType = typeof(VRCAvatarDescriptor);
            targetAvatar.allowSceneObjects = true;
            ObjectField targetAnimatorController = rootVisualElement.Q<ObjectField>(name: "targetAnimatorController");
            targetAnimatorController.objectType = typeof(RuntimeAnimatorController);
            targetAnimatorController.allowSceneObjects = false;

            //isPlayingOrWillChangePlaymode covers if the window was open before entering playmode
            rootVisualElement.EnableInClassList(
                "play-mode",
                EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying);
            Action<PlayModeStateChange> playModeStateChangedCallback = null;
            EditorApplication.playModeStateChanged += playModeStateChangedCallback = s =>
            {
                //this is only here because the ui doesnt get reset when exiting playmode
                if (s == PlayModeStateChange.ExitingPlayMode)
                {
                    rootVisualElement.RemoveFromClassList("play-mode");
                    if (avatarPlayableAnimator != null) Stop();
                    EditorApplication.playModeStateChanged -= playModeStateChangedCallback;
                }
            };

            Button startButton = rootVisualElement.Q<Button>(name: "start");
            startButton.clicked += () =>
            {
                if (avatarPlayableAnimator != null) throw new InvalidOperationException("Should not be possible to start here.");
                avatarPlayableAnimator = new AvatarPlayableAnimator((VRCAvatarDescriptor)targetAvatar.value);
                avatarPlayableAnimator.Attach();

                avatarPlayableAnimator.FX.SetLayerWeight(1, 0);
                avatarPlayableAnimator.FX.SetLayerWeight(2, 0);

                avatarPlayableAnimator.EnterStationNonSitting((RuntimeAnimatorController)targetAnimatorController.value);
                rootVisualElement.AddToClassList("running");
            };
            EnableRunningIfPossible();

            Button stopButton = rootVisualElement.Q<Button>(name: "stop");
            stopButton.clicked += () =>
            {
                if (avatarPlayableAnimator == null) throw new InvalidOperationException("Should not be possible to stop here.");
                Stop();
            };

            targetAvatar.RegisterValueChangedCallback(_ =>
            {
                validAvatarIsTargeted = targetAvatar.value != null;
                EnableRunningIfPossible();
            });
            targetAnimatorController.RegisterValueChangedCallback(_ =>
            {
                animatorControllerIsTargeted = targetAnimatorController.value != null;
                EnableRunningIfPossible();
            });
            void EnableRunningIfPossible()
            {
                startButton.SetEnabled(validAvatarIsTargeted && animatorControllerIsTargeted);
            }
            void Stop()
            {
                avatarPlayableAnimator.Destroy();
                avatarPlayableAnimator = null;
                rootVisualElement.RemoveFromClassList("running");
            }
        }

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

                foreach (var layer in avatar.baseAnimationLayers.Concat(avatar.specialAnimationLayers))
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

                            Action = new AvatarPlayableLayer(8, playableMixer,  GetMask(null));
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

        public static class VrcAvatarMasks
        {
            public static readonly AvatarMask HandsOnly = Load("3a212c1cfe294b64d8edd0aa812eec08");
            public static readonly AvatarMask MuscleOnly = Load("b559a7876dbc2cc48838ac452d3df63f");
            public static readonly AvatarMask Empty = new AvatarMask() { name = "mmdAvatarTester_None" };

            //we use asset ids to work for both old-style projects with an imported .unitypackage
            //and upm packages
            //we could also hypothetically not even use assets and create them in-memory
            //but we have a hard dependency on vrcsdk anyway
            private static AvatarMask Load(string assetGuid)
                => AssetDatabase.LoadAssetAtPath<AvatarMask>(AssetDatabase.GUIDToAssetPath(assetGuid));
        }

        public static class VrcDefaultAnimatorControllers
        {
            public static readonly RuntimeAnimatorController Base = Load("d6bca25210811374a9884e51a498c4f3");
            public static readonly RuntimeAnimatorController Additive = Load("b7fbb0c59d871b54fbaca5bb78ed9e05");
            public static readonly RuntimeAnimatorController Gesture = Load("dd09abeb3b70a7740be388ef7258623f");
            public static readonly RuntimeAnimatorController Action = Load("3fe8b59bcb0c9704aae69e549fe551e9");
            public static readonly RuntimeAnimatorController FX = Load("dd09abeb3b70a7740be388ef7258623f");
            public static readonly RuntimeAnimatorController Sitting = Load("f7a55980b40101140bf92ae96aa8febc");
            public static readonly RuntimeAnimatorController TPose = Load("a76a5d5ebf4884f4cab59f1dfd0e1668");
            public static readonly RuntimeAnimatorController IKPose = Load("accd12269439a4642b7307bdf69c3d99");

            private static RuntimeAnimatorController Load(string assetGuid)
                => AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(assetGuid));
        }

        public class AvatarPlayableLayer
        {
            private AnimationLayerMixerPlayable playableMixer;
            //TODO: having to pass around an index is kinda poop?
            //could encapsulate stuff to hide the index, but that's kinda what we're already setting out to do
            //in any case, either decide to do that, or make this a private member of AvatarPlayableAnimator
            private readonly int inputIndex;

            //part of me doesn't want the mask parameter, but all layers end up using the same logic
            public AvatarPlayableLayer(int inputIndex, AnimationLayerMixerPlayable playableMixer, AvatarMask avatarMask)
            {
                this.playableMixer = playableMixer;
                this.inputIndex = inputIndex;

                if (avatarMask != null) playableMixer.SetLayerMaskFromAvatarMask((uint)inputIndex, avatarMask);
            }

            public void TurnOn() => playableMixer.SetInputWeight(inputIndex, 1);

            public void TurnOff() => playableMixer.SetInputWeight(inputIndex, 0);

            public void BindAnimatorController(RuntimeAnimatorController animatorController)
            {
                AnimatorControllerPlayable playable = AnimatorControllerPlayable.Create(playableMixer.GetGraph(), animatorController);
                playableMixer.ConnectInput(inputIndex, playable, 0, 1);
            }

            public void UnbindAnimator()
            {
                Playable playable = playableMixer.GetInput(inputIndex);
                playableMixer.DisconnectInput(inputIndex);
                if (!playable.IsNull() && playable.CanDestroy()) playable.Destroy();
            }

            public void SetAdditive()
            {
                playableMixer.SetLayerAdditive((uint)inputIndex, true);
            }

            public void SetLayerWeight(int layer, float weight)
            {
                Playable playable = playableMixer.GetInput(inputIndex);
                if (!playable.IsValid()) return;

                AnimatorControllerPlayable animatorControllerPlayable = (AnimatorControllerPlayable)playable;
                animatorControllerPlayable.SetLayerWeight(layer, weight);
            }
        }
    }
}
