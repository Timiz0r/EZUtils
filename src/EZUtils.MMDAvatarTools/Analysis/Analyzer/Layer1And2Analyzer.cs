namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using UnityEditor.Animations;
    using VRC.SDK3.Avatars.Components;

    public class Layer1And2Analyzer : IAnalyzer
    {
        private static readonly HashSet<string> GestureParameters =
            new HashSet<string>(new[] { "GestureLeft", "GestureRight" });

        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            VRCAvatarDescriptor.CustomAnimLayer fxLayer = avatar.baseAnimationLayers.SingleOrDefault(
                l => !l.isDefault && l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            if (fxLayer.animatorController == null) return AnalysisResult.Generate(
                ResultCode.AreGestureLayers,
                AnalysisResultLevel.Pass,
                null);

            List<string> nonGestureTransitionPaths = new List<string>();
            AnimatorController controller = (AnimatorController)fxLayer.animatorController;
            AnalyzeStateMachine(controller.layers[1].stateMachine, nonGestureTransitionPaths);
            AnalyzeStateMachine(controller.layers[2].stateMachine, nonGestureTransitionPaths);

            if (nonGestureTransitionPaths.Count > 0)
            {
                return AnalysisResult.Generate(
                    ResultCode.MayNotBeGestureLayers,
                    AnalysisResultLevel.Warning,
                    null);
            }
            else
            {
                return AnalysisResult.Generate(
                    ResultCode.AreGestureLayers,
                    AnalysisResultLevel.Pass,
                    null);
            }
        }

        private static void AnalyzeStateMachine(
            AnimatorStateMachine stateMachine, List<string> nonGestureTransitionPaths)
        {
            AnimatorState state = stateMachine.defaultState;
            foreach (AnimatorStateTransition transition in state.transitions)
            {
                TraverseChain(
                    state,
                    transition,
                    transition.destinationState,
                    Array.Empty<(AnimatorState sourceState, AnimatorState destinationState)>(),
                    $"({stateMachine}) {state.name}",
                    false);
            }

            AnimatorState dummyAnyState = new AnimatorState() { name = "Any State" };
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                TraverseChain(
                    dummyAnyState,
                    transition,
                    transition.destinationState,
                    Array.Empty<(AnimatorState sourceState, AnimatorState destinationState)>(),
                    $"({stateMachine}) {dummyAnyState.name}",
                    false);
            }

            void TraverseChain(
                AnimatorState previousState,
                AnimatorStateTransition currentTransition,
                AnimatorState currentState,
                (AnimatorState sourceState, AnimatorState destinationState)[] walkedTransitions,
                string path,
                bool hasGestureTransition)
            {
                //more than likely a bug on our end
                if (walkedTransitions.Length > 100) throw new InvalidOperationException("Too many traversals.");

                path = $"{path}->'{currentState.name}'";
                hasGestureTransition = hasGestureTransition
                    || currentTransition.conditions.Any(c => GestureParameters.Contains(c.parameter));

                if (currentState.transitions.Length == 0
                    || walkedTransitions.Contains((previousState, currentState)))
                {
                    if (!hasGestureTransition)
                    {
                        nonGestureTransitionPaths.Add(path.ToString());
                    }
                    return;
                }
                walkedTransitions = walkedTransitions.Append((previousState, currentState)).ToArray();

                foreach (AnimatorStateTransition transition in currentState.transitions)
                {
                    TraverseChain(
                        currentState,
                        transition,
                        transition.destinationState,
                        walkedTransitions,
                        path,
                        hasGestureTransition);
                }
            }
        }

        public static class ResultCode
        {
            public static readonly string AreGestureLayers = Code();
            public static readonly string MayNotBeGestureLayers = Code();

            private static string Code([CallerMemberName] string caller = "")
                => $"{nameof(Layer1And2Analyzer)}.{caller}";
        }
    }
}
