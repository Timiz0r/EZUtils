namespace EZUtils
{
    using AnimLayerType = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType;

    //based on where the design ended up, could argue that the station ones don't need the attributes
    public enum PlayableLayerType
    {
        [PlayableLayerMapping(AnimLayerType.Base)]
        Base,
        [PlayableLayerMapping(AnimLayerType.Additive)]
        Additive,
        [PlayableLayerMapping(AnimLayerType.Sitting)]
        StationSitting,
        [PlayableLayerMapping(AnimLayerType.Sitting)]
        Sitting,
        [PlayableLayerMapping(AnimLayerType.TPose)]
        TPose,
        [PlayableLayerMapping(AnimLayerType.IKPose)]
        IKPose,
        [PlayableLayerMapping(AnimLayerType.Gesture)]
        Gesture,
        [PlayableLayerMapping(AnimLayerType.Action)]
        StationAction,
        [PlayableLayerMapping(AnimLayerType.Action)]
        Action,
        [PlayableLayerMapping(AnimLayerType.FX)]
        FX
    }
}
