namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
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

            foreach (AnimatorState state in animatorControllers
                .SelectMany(c => c.layers)
                .SelectMany(l => l.stateMachine.states)
                .Select(s => s.state))
            {
                //TODO: once rendering is ready, dont short circuit
                if (state.motion == null) return AnalysisResult.Generate(
                    Result.HasEmptyStates,
                    AnalysisResultLevel.Warning,
                    null
                );
            }
            return AnalysisResult.Generate(
                Result.HasNoEmptyStates,
                AnalysisResultLevel.Pass,
                null
            );
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier HasEmptyStates =
                AnalysisResultIdentifier.Create<EmptyStateAnalyzer>("FXレイヤーにはモーションのないステートがあります");
            public static readonly AnalysisResultIdentifier HasNoEmptyStates =
                AnalysisResultIdentifier.Create<EmptyStateAnalyzer>("FXレイヤーの全部のステートにはモーションがあります");
        }
    }
}
