namespace EZUtils
{
    using AnimLayerType = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType;
    using BlendablePlayableLayer = VRC.SDKBase.VRC_PlayableLayerControl.BlendableLayer;
    using BlendableAnimatorLayer = VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer;
    using System;

    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    public sealed class PlayableLayerMappingAttribute : Attribute
    {
        public AnimLayerType LayerType { get; }
        public BlendablePlayableLayer? BlendablePlayableLayer { get; }
        public BlendableAnimatorLayer? BlendableAnimatorLayer { get; }

        public PlayableLayerMappingAttribute(AnimLayerType layerType)
        {
            LayerType = layerType;
            BlendablePlayableLayer = Convert<BlendablePlayableLayer>(layerType);
            BlendableAnimatorLayer = Convert<BlendableAnimatorLayer>(layerType);
        }

        private static T? Convert<T>(AnimLayerType layer) where T : struct, Enum
            => Enum.TryParse(layer.ToString(), out T value) ? value : (T?)null;
    }
}
