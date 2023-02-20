namespace EZUtils.MMDAvatarTools
{
    using System;
    using UnityEditor;
    using UnityEngine;

    //unlike controllers, though these can be mutated, will assume they're just read
    public static class VrcAvatarMasks
    {
        public static readonly AvatarMask HandsOnly = Load("vrc_HandsOnly.mask");
        public static readonly AvatarMask MuscleOnly = Load("vrc_MusclesOnly.mask");
        public static readonly AvatarMask Empty = new AvatarMask() { name = "mmdAvatarTester_None" };

        private static AvatarMask Load(string fileName)
        {
            AvatarMask result = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                $"Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Masks/{fileName}");
            if (result == null)
            {
                result = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                    $"Assets/Samples/VRChat SDK - Avatars/3.0.6/AV3 Demo Assets/Animation/Masks/{fileName}");
            }
            return result != null ? result : throw new InvalidOperationException($"Could not find asset {fileName}'.");
        }
    }
}
