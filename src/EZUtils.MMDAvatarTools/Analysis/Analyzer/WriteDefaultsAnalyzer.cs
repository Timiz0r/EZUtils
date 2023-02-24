namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEditor.Animations;
    using UnityEngine;
    using VRC.SDK3.Avatars.Components;

    public class WriteDefaultsAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            VRCAvatarDescriptor.CustomAnimLayer fxLayer = avatar.baseAnimationLayers.SingleOrDefault(
                l => !l.isDefault && l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            if (fxLayer.animatorController == null) return AnalysisResult.Generate(
                ResultCode.WriteDefaultsEnabled,
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

            const bool DefinitelyDisabled = true;
            ILookup<bool, AnimatorState> writeDefaultsDisabledStates =
                ((AnimatorController)fxLayer.animatorController).layers
                    .SelectMany((l, i) => l.stateMachine.states
                        .Select(s => (
                            layerIndex: i,
                            definitelyDisabled: !hasFXPlayableLayerControlBehavior && !animatorLayerControlLayers.Contains(i),
                            s.state)))
                    .Where(s => s.layerIndex != 1 && s.layerIndex != 2 && !s.state.writeDefaultValues)
                    .ToLookup(s => s.definitelyDisabled, s => s.state);

            if (writeDefaultsDisabledStates.Count == 0) return AnalysisResult.Generate(
                ResultCode.WriteDefaultsEnabled,
                AnalysisResultLevel.Pass,
                null
            );

            List<AnalysisResult> results = new List<AnalysisResult>();
            AnimatorState[] definitelyDisabledStates = writeDefaultsDisabledStates[DefinitelyDisabled]
                .ToArray();
            if (definitelyDisabledStates.Length > 0) results.Add(new AnalysisResult(
                ResultCode.WriteDefaultsDisabled,
                AnalysisResultLevel.Error,
                null
            ));

            AnimatorState[] possiblyDisabledStates = writeDefaultsDisabledStates[!DefinitelyDisabled]
                .ToArray();
            if (possiblyDisabledStates.Length > 0) results.Add(new AnalysisResult(
                ResultCode.WriteDefaultsPotentiallyDisabled,
                AnalysisResultLevel.Warning,
                null
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

        public static class ResultCode
        {
            public static readonly string WriteDefaultsEnabled = Code();
            public static readonly string WriteDefaultsDisabled = Code();
            public static readonly string WriteDefaultsPotentiallyDisabled = Code();

            private static string Code([CallerMemberName] string caller = "")
                => $"{nameof(WriteDefaultsAnalyzer)}.{caller}";
        }
    }
}
