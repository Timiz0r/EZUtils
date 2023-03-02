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
            VRCAvatarDescriptor.CustomAnimLayer fxLayer = avatar.baseAnimationLayers.SingleOrDefault(
                l => !l.isDefault && l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            if (fxLayer.animatorController == null) return AnalysisResult.Create(
                Result.WriteDefaultsEnabled,
                AnalysisResultLevel.Pass,
                null);

            AnimatorStateMachine[] allStateMachines = avatar.baseAnimationLayers
                .Where(l => !l.isDefault && l.animatorController != null)
                .Select(l => (AnimatorController)l.animatorController)
                .SelectMany(c => c.layers.Select(l => l.stateMachine))
                .ToArray();
            //hypothetically should include special layers, but who does that
            bool hasFXPlayableLayerControlBehavior = allStateMachines
                .SelectMany(sm => GetBehaviours(sm))
                .OfType<VRCPlayableLayerControl>()
                .Any(b => b.layer == VRC.SDKBase.VRC_PlayableLayerControl.BlendableLayer.FX && b.goalWeight == 0);
            HashSet<int> animatorLayerControlLayers = new HashSet<int>(allStateMachines
                .SelectMany(sm => GetBehaviours(sm))
                .OfType<VRCAnimatorLayerControl>()
                .Where(b => b.playable == VRC.SDKBase.VRC_AnimatorLayerControl.BlendableLayer.FX && b.goalWeight == 0)
                .Select(b => b.layer)
                .Distinct());

            AnimatorController fxController = (AnimatorController)fxLayer.animatorController;
            ILookup<bool, (string layerName, string stateName)> writeDefaultsDisabledStates =
                fxController.layers
                    .SelectMany((l, i) => l.stateMachine.states
                        .Select(s => (
                            layerName: l.name,
                            layerIndex: i,
                            definitelyDisabled: !hasFXPlayableLayerControlBehavior && !animatorLayerControlLayers.Contains(i),
                            s.state)))
                    .Where(s => s.layerIndex != 1 && s.layerIndex != 2 && !s.state.writeDefaultValues)
                    .ToLookup(s => s.definitelyDisabled, s => (
                        s.layerName,
                        stateName: s.state.name
                    ));

            if (writeDefaultsDisabledStates.Count == 0) return AnalysisResult.Create(
                Result.WriteDefaultsEnabled, AnalysisResultLevel.Pass, new EmptyRenderer());

            //since there can be a mix, we return results for each set (if any)
            const bool DefinitelyDisabled = true;
            List<AnalysisResult> results = new List<AnalysisResult>();
            (string layerName, string stateName)[] definitelyDisabledStates = writeDefaultsDisabledStates[DefinitelyDisabled]
                .ToArray();
            if (definitelyDisabledStates.Length > 0) results.Add(new AnalysisResult(
                Result.WriteDefaultsDisabled,
                AnalysisResultLevel.Error,
                new GeneralRenderer(
                    "FXレイヤーにWrite Defaultsがオフになっているアニメーションステートがあります。" +
                    "しかし、FXレイヤーまたは中のアニメーションレイヤーをオフにする「VRC Animator Layer Control」や「VRC Playable Layer Control」があります。" +
                    "このステートがオフにされる場合、表情が変化できますが、FXレイヤーの他のアニメーションが直前に起動しても無効化にされます。" +
                    "そしてオフにされない場合、表情が変化しない可能性が高くなります。",
                    new AnimatorStateRenderer(
                        "Write Defaultsがオフになっているステート", fxController, definitelyDisabledStates))
            ));

            (string layerName, string stateName)[] possiblyDisabledStates = writeDefaultsDisabledStates[!DefinitelyDisabled]
                .ToArray();
            if (possiblyDisabledStates.Length > 0) results.Add(new AnalysisResult(
                Result.WriteDefaultsPotentiallyDisabled,
                AnalysisResultLevel.Warning,
                new GeneralRenderer(
                    "FXレイヤーにWrite Defaultsがオフになっているアニメーションステートがあります。オンにしないと、表情が変化しない可能性が高くなります。",
                    new AnimatorStateRenderer(
                        "Write Defaultsがオフになっているステート", fxController, definitelyDisabledStates))
            ));

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
