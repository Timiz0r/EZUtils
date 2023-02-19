namespace EZUtils.MMDAvatarTools
{
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;

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
            => playableMixer.SetLayerAdditive((uint)inputIndex, true);

        public void SetLayerWeight(int layer, float weight)
        {
            Playable playable = playableMixer.GetInput(inputIndex);
            if (!playable.IsValid()) return;

            AnimatorControllerPlayable animatorControllerPlayable = (AnimatorControllerPlayable)playable;
            animatorControllerPlayable.SetLayerWeight(layer, weight);
        }
    }
}
