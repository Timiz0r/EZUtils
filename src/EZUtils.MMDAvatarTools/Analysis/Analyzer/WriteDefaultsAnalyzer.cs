namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor.Animations;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    public class WriteDefaultsAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            PlayableLayerInformation playableLayerInformation = PlayableLayerInformation.Generate(avatar);
            ILookup<bool, (string layerName, string stateName)> writeDefaultsDisabledStates = playableLayerInformation.FX.States
                .Where(s =>
                    s.LayerIndex != 1
                    && s.LayerIndex != 2
                    && !s.IsAlwaysDisabled
                    && !s.UnderlyingState.writeDefaultValues)
                .ToLookup(s => s.MayGetDisabledByBehaviour, s => (layerName: s.LayerName, stateName: s.StateName));

            if (writeDefaultsDisabledStates.Count == 0) return AnalysisResult.Create(
                Result.WriteDefaultsEnabled, AnalysisResultLevel.Pass, new EmptyRenderer());

            //since there can be a mix, we return results for each set (if any)
            const bool PossiblyDisabled = true;
            (string layerName, string stateName)[] possiblyDisabledStates = writeDefaultsDisabledStates[PossiblyDisabled]
                .ToArray();
            (string layerName, string stateName)[] definitelyDisabledStates = writeDefaultsDisabledStates[!PossiblyDisabled]
                .ToArray();

            List<AnalysisResult> results = new List<AnalysisResult>();
            if (possiblyDisabledStates.Length > 0) results.Add(new AnalysisResult(
                Result.WriteDefaultsPotentiallyDisabled,
                AnalysisResultLevel.Warning,
                new GeneralRenderer(
                    "FXレイヤーにWrite Defaultsがオフになっているアニメーションステートがあります。" +
                    "しかし、FXレイヤーまたは中のアニメーションレイヤーをオフにする" +
                    "「VRC Animator Layer Control」や「VRC Playable Layer Control」があります。" +
                    "このステートがオフにされる場合、表情が変化できますが、" +
                    "FXレイヤーの他のアニメーションが直前に起動しても無効化にされます。" +
                    "そしてオフにされない場合、表情が変化しない可能性が高くなります。",
                    instructions:
                        "他のアニメーションを使用したい場合、「VRC Animator Layer Control」や「VRC Playable Layer Control」を" +
                        "使わずにWrite Defaultsをオンにしてください。" +
                        "ただし、再生モードやMMDワールドで、アバターやアニメーションがWrite Defaultsの対応ができていることを" +
                        "確認してください。",
                    detailRenderer: new AnimatorStateRenderer(
                        title: "Write Defaultsがオフになっているステート",
                        emptyMessage: "", //we dont output in this case anyway
                        animatorController: playableLayerInformation.FX.UnderlyingController,
                        states: possiblyDisabledStates))
            ));
            if (definitelyDisabledStates.Length > 0) results.Add(new AnalysisResult(
                Result.WriteDefaultsDisabled,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    "FXレイヤーにWrite Defaultsがオフになっているアニメーションステートがあります。" +
                    "オンにしないと、表情が変化しない可能性が高くなります。",
                    instructions:
                        "以下に表示されているステートのWrite Defaultsをオンにしてください。" +
                        "ただし、再生モードやMMDワールドで、アバターやアニメーションがWrite Defaultsの対応ができていることを" +
                        "確認してください。",
                    detailRenderer: new AnimatorStateRenderer(
                        title: "Write Defaultsがオフになっているステート",
                        emptyMessage: "", //we dont output in this case anyway
                        animatorController: playableLayerInformation.FX.UnderlyingController,
                        states: definitelyDisabledStates))));

            return results;

            IEnumerable<AnimatorState> GetStates(AnimatorStateMachine sm)
                => sm.states
                    .Select(s => s.state)
                    .Concat(sm.stateMachines
                        .Select(sm2 => sm2.stateMachine)
                        .SelectMany(sm2 => GetStates(sm2)));
            IEnumerable<StateMachineBehaviour> GetBehaviours(AnimatorStateMachine sm)
                => sm.behaviours
                    .Concat(sm.states.SelectMany(s => s.state.behaviours))
                    .Concat(sm.stateMachines
                        .Select(sm2 => sm2.stateMachine)
                        .SelectMany(sm2 => GetBehaviours(sm2)));

        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier WriteDefaultsEnabled =
                AnalysisResultIdentifier.Create<WriteDefaultsAnalyzer>("FXレイヤーのWrite Defaultsがオン");
            public static readonly AnalysisResultIdentifier WriteDefaultsDisabled =
                AnalysisResultIdentifier.Create<WriteDefaultsAnalyzer>("FXレイヤーのWrite Defaultsがオフ");
            public static readonly AnalysisResultIdentifier WriteDefaultsPotentiallyDisabled =
                AnalysisResultIdentifier.Create<WriteDefaultsAnalyzer>("FXレイヤーのWrite Defaultsがオフになっている可能性があります");
        }
    }
}
