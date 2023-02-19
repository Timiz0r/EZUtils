namespace EZUtils.MMDAvatarTools
{
    using UnityEditor;
    using UnityEngine;

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
}
