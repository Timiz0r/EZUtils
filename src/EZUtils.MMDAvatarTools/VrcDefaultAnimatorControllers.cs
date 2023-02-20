namespace EZUtils.MMDAvatarTools
{
    using System;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;

    //is not static to allow workflows where these loaded assets are then modified.
    //would not one consumer to affect another consumer.
    public class VrcDefaultAnimatorControllers
    {
        //using asset ids to be vcc/old unitypackage agnostic
        private readonly Lazy<AnimatorController> @base =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3LocomotionLayer.controller"));
        private readonly Lazy<AnimatorController> additive =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3IdleLayer.controller"));
        private readonly Lazy<AnimatorController> gesture =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3HandsLayer.controller"));
        private readonly Lazy<AnimatorController> action =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3ActionLayer.controller"));
        private readonly Lazy<AnimatorController> fx =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3HandsLayer.controller"));
        private readonly Lazy<AnimatorController> sitting =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3SittingLayer.controller"));
        private readonly Lazy<AnimatorController> tPose =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3UtilityTPose.controller"));
        private readonly Lazy<AnimatorController> ikPose =
            new Lazy<AnimatorController>(() => Load("vrc_AvatarV3UtilityIKPose.controller"));

        public AnimatorController Base => @base.Value;
        public AnimatorController Additive => additive.Value;
        public AnimatorController Gesture => gesture.Value;
        public AnimatorController Action => action.Value;
        public AnimatorController FX => fx.Value;
        public AnimatorController Sitting => sitting.Value;
        public AnimatorController TPose => tPose.Value;
        public AnimatorController IKPose => ikPose.Value;

        private static AnimatorController Load(string fileName)
        {
            AnimatorController result = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                $"Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Controllers/{fileName}");
            if (result == null)
            {
                result = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    $"Assets/Samples/VRChat SDK - Avatars/3.0.6/AV3 Demo Assets/Animation/Controllers/{fileName}");
            }
            return result != null ? result : throw new InvalidOperationException($"Could not find asset {fileName}'.");
        }
    }
}
