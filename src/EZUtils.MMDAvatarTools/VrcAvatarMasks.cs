namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    //unlike controllers, though these can be mutated, will assume they're just read
    public static class VrcAvatarMasks
    {
        public static readonly AvatarMask HandsOnly = Load("vrc_HandsOnly.mask");
        public static readonly AvatarMask MuscleOnly = Load("vrc_MusclesOnly.mask");
        public static readonly AvatarMask NoHumanoid = CreateNoHumanoidMask();

        private static AvatarMask Load(string fileName)
        {
            AvatarMask original = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                $"Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/Masks/{fileName}");
            if (original == null)
            {
                original = AssetDatabase.LoadAssetAtPath<AvatarMask>(
                    $"Assets/Samples/VRChat SDK - Avatars/3.0.6/AV3 Demo Assets/Animation/Masks/{fileName}");
            }

            if (original == null) throw new InvalidOperationException($"Could not find asset {fileName}'.");

            AvatarMask copy = new AvatarMask();
            EditorUtility.CopySerialized(original, copy);
            return copy;
        }

        private static AvatarMask CreateNoHumanoidMask()
        {
            AvatarMask mask = new AvatarMask() { name = "mmdAvatarTester_NoHumanoid" };
            foreach (AvatarMaskBodyPart bodyPart in Enum.GetValues(typeof(AvatarMaskBodyPart)).Cast<AvatarMaskBodyPart>())
            {
                if (bodyPart == AvatarMaskBodyPart.LastBodyPart) continue;
                mask.SetHumanoidBodyPartActive(bodyPart, false);
            }
            return mask;
        }
    }
}
