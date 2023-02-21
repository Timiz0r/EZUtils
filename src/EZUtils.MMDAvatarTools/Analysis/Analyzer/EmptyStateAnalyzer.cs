namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEditor.Animations;
    using VRC.SDK3.Avatars.Components;

    //still don't really know why empty states  cause issues, but they always seem to. hence, cant really go harder than warn.
    public class EmptyStateAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            IEnumerable<AnimatorController> animatorControllers = Enumerable.Empty<AnimatorController>();
            if (avatar.baseAnimationLayers != null)
            {
                animatorControllers = animatorControllers.Concat(
                    avatar.baseAnimationLayers
                    .Select(l => (AnimatorController)l.animatorController)
                    .Where(c => c != null));
            }
            if (avatar.specialAnimationLayers != null)
            {
                animatorControllers = animatorControllers.Concat(
                    avatar.specialAnimationLayers
                    .Select(l => (AnimatorController)l.animatorController)
                    .Where(c => c != null));
            }

            //TODO:
            //the awkward thing is that controllers can have unconnected states and states not in layers or
            //sub state machines, effectively invisible. both cases probably don't cause issues,
            //so we ideally ignore them. since we're just warning, this is okay for now. or maybe the do have an effect idk.
            //though rather the asset can have states not in layers, but they wouldnt show up here, i suppose.
            foreach (AnimatorState state in animatorControllers
                .SelectMany(c => c.layers)
                .SelectMany(l => l.stateMachine.states)
                .Select(s => s.state))
            {
                //TODO: once rendering is ready, dont short circuit
                if (state.motion == null) return AnalysisResult.Generate(
                    ResultCode.HasEmptyStates,
                    AnalysisResultLevel.Warning,
                    null
                );
            }
            return AnalysisResult.Generate(
                ResultCode.HasNoEmptyStates,
                AnalysisResultLevel.Pass,
                null
            );
        }

        public static class ResultCode
        {
            public static readonly string HasEmptyStates = Code();
            public static readonly string HasNoEmptyStates = Code();

            private static string Code([CallerMemberName] string caller = "")
                => $"{nameof(EmptyStateAnalyzer)}.{caller}";
        }
    }
}
