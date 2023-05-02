namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor.Animations;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;
    using static Localization;

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
                    T("The FX layer has states with write defaults off. " +
                    "However, the avatar has a 'VRC Animator Layer Control' or 'VRC Playable Layer Control' " +
                    "that will either turn off the containing animation layer or the entire FX layer. " +
                    "If such states are turned off, facial expressions can change; " +
                    "however, the animations of these states, even when played beforehand, will not work. " +
                    "Furthermore, if not turned off, facial animations likely won't change."),
                    instructions: T(
                        "To ensure these animations continue to work, instead of using 'VRC Animator Layer Control' or 'VRC Playable Layer Control', " +
                        "prefer turning Write Defaults on. " +
                        "Be sure to verify that the animations work fine with Write Defaults on by testing them " +
                        "in play mode or in MMD worlds."),
                    detailRenderer: new AnimatorStateRenderer(
                        title: T("States with Write Defaults on"),
                        emptyMessage: "", //we dont output in this case anyway
                        animatorController: playableLayerInformation.FX.UnderlyingController,
                        states: possiblyDisabledStates))
            ));
            if (definitelyDisabledStates.Length > 0) results.Add(new AnalysisResult(
                Result.WriteDefaultsDisabled,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    T("The FX layer has states with write defaults off. " +
                    "If they are not turned on, facial expressions will likely not change."),
                    instructions: T(
                        "Turn on Write Defaults for the below states." +
                        "Be sure to verify that the animations work fine with Write Defaults on by testing them " +
                        "in play mode or in MMD worlds."),
                    detailRenderer: new AnimatorStateRenderer(
                        title: T("States with Write Defaults on"),
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
                AnalysisResultIdentifier.Create<WriteDefaultsAnalyzer>(T("FX layer states have Write Defaults on"));
            public static readonly AnalysisResultIdentifier WriteDefaultsDisabled =
                AnalysisResultIdentifier.Create<WriteDefaultsAnalyzer>(T("FX layer states have Write Defaults off"));
            public static readonly AnalysisResultIdentifier WriteDefaultsPotentiallyDisabled =
                AnalysisResultIdentifier.Create<WriteDefaultsAnalyzer>(T("FX layer states have Write Defaults possibly off"));
        }
    }
}
