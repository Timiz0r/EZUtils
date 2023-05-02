namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;
    using static Localization;

    public class HumanoidAnimationAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            PlayableLayerInformation playableLayerInformation = PlayableLayerInformation.Generate(avatar);
            if (playableLayerInformation.FX.ConfiguredMask == null) return AnalysisResult.Create(
                Result.NoActiveHumanoidAnimationsFound,
                AnalysisResultLevel.Pass,
                new GeneralRenderer(T("Even if there was a humanoid animation, the default avatar mask would disable it.")));

            AvatarMaskBodyPart[] activeBodyParts = Enum.GetValues(typeof(AvatarMaskBodyPart))
                .Cast<AvatarMaskBodyPart>()
                .Take((int)AvatarMaskBodyPart.LastBodyPart)
                .Where(bp => playableLayerInformation.FX.ConfiguredMask.GetHumanoidBodyPartActive(bp))
                .ToArray();

            if (activeBodyParts.Length == 0) return AnalysisResult.Create(
                Result.NoActiveHumanoidAnimationsFound,
                AnalysisResultLevel.Pass,
                new GeneralRenderer(T("Even if there was a humanoid animation, the configured avatar mask would disable it.")));

            //no matter what's in the mask, or in a specific layer's mask, if a layer has a single humanoid animation
            //curve of any sort, then it'll cause the body to not move according to the mmd motion.
            //this makes detection easy, at least, since we don't have to dig into matching up curves to masked parts.
            ILookup<bool, (string layerName, string stateName)> humanMotionStates = playableLayerInformation.FX.States
                .Where(s => s.LayerIndex != 1 && s.LayerIndex != 2 && !s.IsAlwaysDisabled)
                .Where(s => s.UnderlyingState.motion != null)
                .Where(s => s.UnderlyingState.motion.isHumanMotion)
                .ToLookup(s => s.MayGetDisabledByBehaviour, s => (layerName: s.LayerName, stateName: s.StateName));

            if (humanMotionStates.Count == 0) return AnalysisResult.Create(
                Result.NoActiveHumanoidAnimationsFound,
                AnalysisResultLevel.Pass,
                new GeneralRenderer(
                    T("The configured avatar mask has body parts added. " +
                    "However, because there are no humanoid animations.")));

            List<AnalysisResult> results = new List<AnalysisResult>();
            const bool PossiblyDisabled = true;
            (string layerName, string stateName)[] possiblyActiveStates = humanMotionStates[PossiblyDisabled].ToArray();
            (string layerName, string stateName)[] definitelyActiveStates = humanMotionStates[!PossiblyDisabled].ToArray();

            if (possiblyActiveStates.Length > 0) results.Add(new AnalysisResult(
                Result.PossiblyActiveHumanoidAnimationsFound,
                AnalysisResultLevel.Warning,
                new GeneralRenderer(
                    T("There is a humanoid animation in use by the FX layer. " +
                    "However, the avatar has a 'VRC Animator Layer Control' or 'VRC Playable Layer Control' " +
                    "that will either turn off the containing animation layer or the entire FX layer. " +
                    "As long the layer containing the animation is turned off, MMD animations will play normally. " +
                    "If not turned off, MMD animations will not play."),
                    instructions:
                        T("Check the instructions for the abatar. Usually, there is a toggle, if not done automatically."),
                    detailRenderer: new AnimatorStateRenderer(
                        title: T("States containing humanoid animations"),
                        emptyMessage: "", //we don't output in this case, anyway
                        animatorController: playableLayerInformation.FX.UnderlyingController,
                        states: possiblyActiveStates))
            ));
            if (definitelyActiveStates.Length > 0) results.Add(new AnalysisResult(
                Result.ActiveHumanoidAnimationsFound,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    T("Due to humanoid animations found in the FX layer, MMD animations will not play."),
                    instructions:
                        T("There are various ways to fix the issue, but the normal way is to first move the animations to the Gesture layer, " +
                        "then ensure there is no avatar mask set in the FX layer's first animation layer."),
                    detailRenderer: new AnimatorStateRenderer(
                        title: T("States containing humanoid animations"),
                        emptyMessage: "", //we don't output in this case, anyway
                        animatorController: playableLayerInformation.FX.UnderlyingController,
                        states: definitelyActiveStates))
            ));
            return results;
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier ActiveHumanoidAnimationsFound =
                AnalysisResultIdentifier.Create<HumanoidAnimationAnalyzer>(T("Active humanoid animations found"));
            public static readonly AnalysisResultIdentifier PossiblyActiveHumanoidAnimationsFound =
                AnalysisResultIdentifier.Create<HumanoidAnimationAnalyzer>(T("Possibly active humanoid animations found"));
            public static readonly AnalysisResultIdentifier NoActiveHumanoidAnimationsFound =
                AnalysisResultIdentifier.Create<HumanoidAnimationAnalyzer>(T("Active humanoid animations not found"));
        }
    }
}
