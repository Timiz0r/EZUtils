namespace EZUtils.MMDAvatarTools
{
    using UnityEditor;
    using UnityEngine;

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
}
