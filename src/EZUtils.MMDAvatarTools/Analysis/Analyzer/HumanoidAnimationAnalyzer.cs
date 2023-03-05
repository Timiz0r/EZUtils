namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    public class HumanoidAnimationAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            PlayableLayerInformation playableLayerInformation = PlayableLayerInformation.Generate(avatar);
            if (playableLayerInformation.FX.ConfiguredMask == null) return AnalysisResult.Create(
                Result.NoActiveHumanoidAnimationsFound,
                AnalysisResultLevel.Pass,
                new GeneralRenderer("ヒューマノイドのアニメーションがあっても、FXレイヤーのディフォルトのアバターマスクに無効化されてます。"));

            AvatarMaskBodyPart[] activeBodyParts = Enum.GetValues(typeof(AvatarMaskBodyPart))
                .Cast<AvatarMaskBodyPart>()
                .Take((int)AvatarMaskBodyPart.LastBodyPart)
                .Where(bp => playableLayerInformation.FX.ConfiguredMask.GetHumanoidBodyPartActive(bp))
                .ToArray();

            if (activeBodyParts.Length == 0) return AnalysisResult.Create(
                Result.NoActiveHumanoidAnimationsFound,
                AnalysisResultLevel.Pass,
                new GeneralRenderer("ヒューマノイドのアニメーションがあっても、設定されてるアバターマスクに無効化されてます。"));

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
                new GeneralRenderer("設定されてるアバターマスクにはボディ部分がくわえています。それでも、ヒューマノイドのアニメーションがありません。"));

            List<AnalysisResult> results = new List<AnalysisResult>();
            const bool PossiblyDisabled = true;
            (string layerName, string stateName)[] possiblyActiveStates = humanMotionStates[PossiblyDisabled].ToArray();
            (string layerName, string stateName)[] definitelyActiveStates = humanMotionStates[!PossiblyDisabled].ToArray();

            if (possiblyActiveStates.Length > 0) results.Add(new AnalysisResult(
                Result.PossiblyActiveHumanoidAnimationsFound,
                AnalysisResultLevel.Warning,
                new GeneralRenderer(
                    "FXレイヤーに使われてるヒューマノイドのアニメーションがあります。" +
                    "しかし、FXレイヤーまたは中のアニメーションレイヤーをオフにする「VRC Animator Layer Control」や「VRC Playable Layer Control」があります。" +
                    "このアニメーションを持っているレイヤーがオフにされる場合、MMDのアニメーションが再生します。" +
                    "そしてオフにされない場合、MMDのアニメーションが再生しません。",
                    detailRenderer: new AnimatorStateRenderer(
                        title: "ヒューマノイドのアニメーションがあるステート",
                        emptyMessage: "", //we don't output in this case, anyway
                        animatorController: playableLayerInformation.FX.UnderlyingController,
                        states: possiblyActiveStates))
            ));
            if (definitelyActiveStates.Length > 0) results.Add(new AnalysisResult(
                Result.ActiveHumanoidAnimationsFound,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    "FXレイヤーに使われてるヒューマノイドのアニメーションがあることによって、MMDのアニメーションが再生しません。",
                    detailRenderer: new AnimatorStateRenderer(
                        title: "ヒューマノイドのアニメーションがあるステート",
                        emptyMessage: "", //we don't output in this case, anyway
                        animatorController: playableLayerInformation.FX.UnderlyingController,
                        states: definitelyActiveStates))
            ));
            return results;
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier ActiveHumanoidAnimationsFound =
                AnalysisResultIdentifier.Create<HumanoidAnimationAnalyzer>("使われてるヒューマノイドのアニメーションを発見");
            public static readonly AnalysisResultIdentifier PossiblyActiveHumanoidAnimationsFound =
                AnalysisResultIdentifier.Create<HumanoidAnimationAnalyzer>("使われてる可能性のあるヒューマノイドのアニメーションを発見");
            public static readonly AnalysisResultIdentifier NoActiveHumanoidAnimationsFound =
                AnalysisResultIdentifier.Create<HumanoidAnimationAnalyzer>("使われてるヒューマノイドのアニメーションが未発見");
        }
    }
}
