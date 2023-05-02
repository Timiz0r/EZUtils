namespace EZUtils.MMDAvatarTools
{
    using System;
    using UnityEditor.Animations;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;
    using static Localization;

    public class MmdAvatarTester
    {
        private AvatarPlayableAnimator avatarPlayableAnimator;

        public void Start(VRCAvatarDescriptor avatar, AnimationClip animation)
        {
            if (avatarPlayableAnimator != null) throw new InvalidOperationException(
                T("Attempted to start while already started."));

            avatarPlayableAnimator = AvatarPlayableAnimator.Attach(avatar);
            avatarPlayableAnimator.Start();

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
            avatarPlayableAnimator?.Detach();
            avatarPlayableAnimator = null;
        }
    }
}
