namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor.Animations;
    using VRC.SDK3.Avatars.Components;
    using static Localization;

    //the design is currently a bit weird in that there are different results for each layer, and we use logic
    //to select the right ones based on index. could be worth a redesign in the future, but not worth it at the moment.

    public class Layer1And2Analyzer : IAnalyzer
    {
        private static readonly HashSet<string> GestureParameters =
            new HashSet<string>(new[] { "GestureLeft", "GestureRight" });

        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            PlayableLayerInformation playableLayerInformation = PlayableLayerInformation.Generate(avatar);
            if (playableLayerInformation.FX.UnderlyingController == null) return new[]
            {
                new AnalysisResult(
                    Result.Layer1_IsGestureLayer,
                    AnalysisResultLevel.Pass,
                    new GeneralRenderer(T("No FX layer found"))),
                new AnalysisResult(
                    Result.Layer2_IsGestureLayer,
                    AnalysisResultLevel.Pass,
                    new GeneralRenderer(T("No FX layer found"))),
            };

            List<AnalysisResult> results = new List<AnalysisResult>(2)
            {
                AnalyzeStateMachine(playableLayerInformation, 1),
                AnalyzeStateMachine(playableLayerInformation, 2)
            };

            return results;
        }

        private static AnalysisResult AnalyzeStateMachine(
            PlayableLayerInformation playableLayerInformation, int layerIndex)
        {
            AnimatorController controller = playableLayerInformation.FX.UnderlyingController;
            if (controller.layers.Length <= layerIndex) return new AnalysisResult(
                layerIndex == 1 ? Result.Layer1_IsGestureLayer : Result.Layer2_IsGestureLayer,
                AnalysisResultLevel.Pass,
                new GeneralRenderer(T($"The FX layer's layer {layerIndex} does not exist.")));

            //TODO: output these
            List<string> nonGestureTransitionPaths = new List<string>();

            AnimatorStateMachine stateMachine = controller.layers[layerIndex].stateMachine;
            AnimatorState state = stateMachine.defaultState;
            foreach (AnimatorStateTransition transition in state.transitions)
            {
                TraverseChain(
                    state,
                    transition,
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
                    Array.Empty<(AnimatorState sourceState, AnimatorState destinationState)>(),
                    $"({stateMachine}) {dummyAnyState.name}",
                    false);
            }
            if (nonGestureTransitionPaths.Count > 0)
            {
                (string layerName, string stateName)[] states = playableLayerInformation.FX.States
                    .Where(s => s.LayerIndex == layerIndex)
                    .Select(s => (layerName: s.LayerName, stateName: s.StateName))
                    .ToArray();
                return new AnalysisResult(
                    layerIndex == 1 ? Result.Layer1_MayNotBeGestureLayer : Result.Layer2_MayNotBeGestureLayer,
                    AnalysisResultLevel.Warning,
                    new GeneralRenderer(
                        T("MMD worlds will turn off the FX layer's first and second layers. " +
                        "If there are important, non-gesture animations in these layers, strange behaviors may occur."),
                        instructions:
                            T("Confirm that the below states, even when turned off, will not cause any problems.") +
                            T($"If they are problematic, consider making layer {layerIndex} an empty layer."),
                        detailRenderer: new AnimatorStateRenderer(
                            title: T($"The FX layer's animation layer {layerIndex}"),
                            emptyMessage: T($"There are no states in the FX layer's animation layer {layerIndex}."),
                            animatorController: controller,
                            states: states)));
            }
            else
            {
                return new AnalysisResult(
                    layerIndex == 1 ? Result.Layer1_IsGestureLayer : Result.Layer2_IsGestureLayer,
                    AnalysisResultLevel.Pass,
                    new GeneralRenderer(T($"The FX layer's animation layer {layerIndex} appears to be gesture-style.")));
            }

            void TraverseChain(
                AnimatorState previousState,
                AnimatorStateTransition currentTransition,
                (AnimatorState sourceState, AnimatorState destinationState)[] walkedTransitions,
                string path,
                bool hasGestureTransition)
            {
                //more than likely a bug on our end
                if (walkedTransitions.Length > 100) throw new InvalidOperationException(T("Too many traversals."));

                hasGestureTransition = hasGestureTransition
                    || currentTransition.conditions.Any(c => GestureParameters.Contains(c.parameter));

                string currentPart = currentTransition.destinationStateMachine != null
                    ? $"State machine {currentTransition.destinationStateMachine.name} (not yet supported)"
                    : currentTransition.isExit
                        ? "Exit"
                        : currentTransition.destinationState.name;
                path = $"{path}->'{currentPart}'";

                if (currentTransition.destinationStateMachine != null //TODO: dont support them yet
                    || currentTransition.isExit
                    || currentTransition.destinationState.transitions.Length == 0
                    || walkedTransitions.Contains((previousState, currentTransition.destinationState)))
                {
                    if (!hasGestureTransition)
                    {
                        nonGestureTransitionPaths.Add(path);
                    }
                    return;
                }
                walkedTransitions = walkedTransitions.Append((previousState, currentTransition.destinationState)).ToArray();

                foreach (AnimatorStateTransition transition in currentTransition.destinationState.transitions)
                {
                    TraverseChain(
                        currentTransition.destinationState,
                        transition,
                        walkedTransitions,
                        path,
                        hasGestureTransition);
                }
            }
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier Layer1_IsGestureLayer =
                AnalysisResultIdentifier.Create<Layer1And2Analyzer>("FX layer animation layer 1 is gesture-style");
            public static readonly AnalysisResultIdentifier Layer1_MayNotBeGestureLayer =
                AnalysisResultIdentifier.Create<Layer1And2Analyzer>("FX layer animation layer 1 may not be gesture-style");
            public static readonly AnalysisResultIdentifier Layer2_IsGestureLayer =
                AnalysisResultIdentifier.Create<Layer1And2Analyzer>("FX layer animation layer 2 is gesture-style");
            public static readonly AnalysisResultIdentifier Layer2_MayNotBeGestureLayer =
                AnalysisResultIdentifier.Create<Layer1And2Analyzer>("FX layer animation layer 2 may not be gesture-style");
        }
    }
}
