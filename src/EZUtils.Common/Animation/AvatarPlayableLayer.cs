namespace EZUtils
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;

    [DebuggerDisplay("{playableLayerType}")]
    public class AvatarPlayableLayer
    {
        private AnimationLayerMixerPlayable playableMixer;
        private AnimatorControllerPlayable animatorControllerPlayable;
        private LayerControlPlayableBehaviour layerControl;
        private readonly PlayableLayerType playableLayerType;
        private readonly int mixerIndex;

        //part of me doesn't want the mask parameter, but all layers end up using the same logic
        public AvatarPlayableLayer(PlayableLayerType playableLayerType, AnimationLayerMixerPlayable playableMixer, AvatarMask avatarMask)
        {
            this.playableMixer = playableMixer;
            this.playableLayerType = playableLayerType;
            mixerIndex = (int)playableLayerType;

            if (avatarMask != null) playableMixer.SetLayerMaskFromAvatarMask((uint)mixerIndex, avatarMask);
        }

        public void TurnOn() => playableMixer.SetInputWeight(mixerIndex, 1);

        public void TurnOff() => playableMixer.SetInputWeight(mixerIndex, 0);

        public void BindAnimatorController(RuntimeAnimatorController animatorController)
        {
            PlayableGraph graph = playableMixer.GetGraph();
            animatorControllerPlayable =
                AnimatorControllerPlayable.Create(graph, animatorController);


            ScriptPlayable<LayerControlPlayableBehaviour> layerControlPlayable =
                ScriptPlayable<LayerControlPlayableBehaviour>.Create(graph, 1);
            layerControl = layerControlPlayable.GetBehaviour();
            layerControl.playableLayer = this;
            layerControlPlayable.ConnectInput(0, animatorControllerPlayable, 0, 1);
            playableMixer.ConnectInput(mixerIndex, layerControlPlayable, 0, 1);
        }

        public void UnbindAnimator()
        {
            playableMixer.DisconnectInput(mixerIndex);
            if (!animatorControllerPlayable.IsNull()
                && animatorControllerPlayable.CanDestroy()) animatorControllerPlayable.Destroy();
        }

        public void SetAdditive()
            => playableMixer.SetLayerAdditive((uint)mixerIndex, true);

        public float Weight => !animatorControllerPlayable.IsValid() ? 1 : playableMixer.GetInputWeight(mixerIndex);
        public float GetLayerWeight(int layer) => !animatorControllerPlayable.IsValid() ? 1 : animatorControllerPlayable.GetLayerWeight(layer);

        public void SetWeight(float weight)
        {
            if (!animatorControllerPlayable.IsValid()) return;
            playableMixer.SetInputWeight(mixerIndex, weight);
        }
        public void SetLayerWeight(int layer, float weight)
        {
            if (!animatorControllerPlayable.IsValid()) return;
            animatorControllerPlayable.SetLayerWeight(layer, weight);
        }

        public void SetGoalWeight(float goalWeight, float blendDuration)
            => layerControl?.SetPlayableLayerGoalWeight(goalWeight, blendDuration);
        public void SetLayerGoalWeight(int layer, float goalWeight, float blendDuration)
            => layerControl?.SetAnimatorLayerGoalWeight(layer, goalWeight, blendDuration);

        private class LayerControlPlayableBehaviour : PlayableBehaviour
        {
            private GoalWeight playableLayerGoalWeight = GoalWeight.AtGoal;
            private readonly Dictionary<int, GoalWeight> animatorLayerGoalWeight = new Dictionary<int, GoalWeight>();

            public AvatarPlayableLayer playableLayer;

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                base.PrepareFrame(playable, info);
                float time = (float)playable.GetTime();

                if (playableLayerGoalWeight.HasNewWeight(
                    currentTime: time,
                    currentWeight: playableLayer.Weight,
                    out float newPlayableLayerWeight))
                {
                    playableLayer.SetWeight(newPlayableLayerWeight);
                }

                foreach (KeyValuePair<int, GoalWeight> layer in animatorLayerGoalWeight)
                {
                    if (layer.Value.HasNewWeight(
                        currentTime: time,
                        currentWeight: playableLayer.GetLayerWeight(layer.Key),
                        out float newLayerWeight))
                    {
                        playableLayer.SetLayerWeight(layer.Key, newLayerWeight);
                    }
                }
            }

            public void SetPlayableLayerGoalWeight(float goalWeight, float blendDuration)
                => playableLayerGoalWeight = new GoalWeight(goalWeight, blendDuration);
            public void SetAnimatorLayerGoalWeight(int layer, float goalWeight, float blendDuration)
                => animatorLayerGoalWeight[layer] = new GoalWeight(goalWeight, blendDuration);
        }

        private class GoalWeight
        {
            public static readonly GoalWeight AtGoal = GetAtGoalGoalWeight();

            private readonly float goalWeight;
            public readonly float blendDuration;
            private float endTime = float.MaxValue;
            private float startWeight;
            //we cant just compare time, since there will likely be one case where current time is past end time
            //and we havent given out the goal weight yet.
            private bool goalHasBeenReached = false;

            public GoalWeight(float goalWeight, float blendDuration)
            {
                this.goalWeight = goalWeight;
                this.blendDuration = blendDuration;
            }

            public bool HasNewWeight(float currentTime, float currentWeight, out float weight)
            {
                if (goalHasBeenReached)
                {
                    weight = currentWeight;
                    return false;
                }

                if (endTime == float.MaxValue)
                {
                    endTime = currentTime + blendDuration;
                    startWeight = currentWeight;
                }
                goalHasBeenReached = currentTime >= endTime;

                weight = Mathf.Lerp(startWeight, goalWeight, currentTime / endTime);

                return true;
            }

            private static GoalWeight GetAtGoalGoalWeight()
            {
                GoalWeight result = new GoalWeight(1, 0)
                {
                    goalHasBeenReached = true
                };
                return result;
            }
        }
    }
}
