namespace EZUtils.MMDAvatarTools
{
    using System;
    using UnityEditor.Animations;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    //TODO: note that, in hindsight, a better design might be to turn this back into a non-component, since we don't
    //need it to be a component. it was originally done for playable/animator layer control, but that ended up
    //being its own component in order to support AvatarPlayableAnimator

    //should not be addable in editor
    [AddComponentMenu("")]
    public class MmdAvatarTester : MonoBehaviour
    {
        private AvatarPlayableAnimator avatarPlayableAnimator;

        public void StartTesting(AnimationClip animation)
        {
            if (avatarPlayableAnimator != null) throw new InvalidOperationException(
                "Attempted to start while already started.");

            VRCAvatarDescriptor avatar = GetComponent<VRCAvatarDescriptor>();
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

        public void OnDestroy()
        {
            avatarPlayableAnimator?.Detach();
            avatarPlayableAnimator = null;
        }

        public void OnDisable()
        {
            avatarPlayableAnimator?.Stop();
        }

        public void OnEnable()
        {
            //one awkward thing is that if destroyed in inspector, enabling wont do anything because we detached
            //and nullified the animator. users shouldn't do it, so this should be fine.
            avatarPlayableAnimator?.Start();
        }
    }
}