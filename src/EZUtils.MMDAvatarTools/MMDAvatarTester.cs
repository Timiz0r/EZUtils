namespace EZUtils.MMDAvatarTools
{
    using System;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    public class MMDAvatarTester
    {
        private AvatarPlayableAnimator avatarPlayableAnimator = null;

        public void Start(VRCAvatarDescriptor avatar, AnimationClip animation)
        {
            if (avatarPlayableAnimator != null) throw new InvalidOperationException(
                "Attempted to start while already started.");

            avatarPlayableAnimator = new AvatarPlayableAnimator(avatar);
            avatarPlayableAnimator.Attach();

            avatarPlayableAnimator.FX.SetLayerWeight(1, 0);
            avatarPlayableAnimator.FX.SetLayerWeight(2, 0);

            RuntimeAnimatorController animatorController = CreateAnimatorController();
            avatarPlayableAnimator.EnterStationNonSitting(animatorController);

            RuntimeAnimatorController CreateAnimatorController()
            {
                AnimatorController result = new AnimatorController();
                result.AddLayer("Layer");
                _ = result.AddMotion(animation);
                return result;
            }
        }

        public void Stop()
        {
            avatarPlayableAnimator?.Destroy();
            avatarPlayableAnimator = null;
        }
    }
}
