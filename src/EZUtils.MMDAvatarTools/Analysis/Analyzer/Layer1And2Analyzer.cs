namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
            if (fxLayer.animatorController == null) return AnalysisResult.Create(
                Result.AreGestureLayers,
                AnalysisResultLevel.Pass,
                null);

            List<string> nonGestureTransitionPaths = new List<string>();
            AnimatorController controller = (AnimatorController)fxLayer.animatorController;
            AnalyzeStateMachine(controller.layers[1].stateMachine, nonGestureTransitionPaths);
            AnalyzeStateMachine(controller.layers[2].stateMachine, nonGestureTransitionPaths);

            if (nonGestureTransitionPaths.Count > 0)
            {
                (string layerName, string stateName)[] layer1and2States = controller.layers
                    .SelectMany((l, i) => l.stateMachine.states
                        .Select(s => (
                            layerIndex: i,
                            layerName: l.name,
                            stateName: s.state.name
                        )))
                    .Where(s => s.layerIndex == 1 || s.layerIndex == 2)
                    .Select(s => (s.layerName, s.stateName))
                    .ToArray();
                return AnalysisResult.Create(
                    Result.MayNotBeGestureLayers,
                    AnalysisResultLevel.Warning,
                    new GeneralRenderer(
                        "MMDワールドはFXレイヤーの第1と第2レイヤーをオフにしますので、" +
                        "ジェスチャー様なアニメーション以外があっていたら、異変が起こる可能性があります。",
                        new AnimatorStateRenderer(
                            title: "FXレイヤーのアニメーションレイヤー",
                            emptyMessage: "アニメーションレイヤーがFXレイヤーに存在しません。",
                            animatorController: controller,
                            states: layer1and2States)));
            }
            else
            {
                return AnalysisResult.Create(
                    Result.AreGestureLayers,
                    AnalysisResultLevel.Pass,
                    new GeneralRenderer("FXレイヤーの第1と第2レイヤーがジェスチャー様になっているようです。"));
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

            void TraverseChain(
                AnimatorState previousState,
                AnimatorStateTransition currentTransition,
                (AnimatorState sourceState, AnimatorState destinationState)[] walkedTransitions,
                string path,
                bool hasGestureTransition)
            {
                //more than likely a bug on our end
                if (walkedTransitions.Length > 100) throw new InvalidOperationException("Too many traversals.");

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
            public static readonly AnalysisResultIdentifier AreGestureLayers =
                AnalysisResultIdentifier.Create<Layer1And2Analyzer>("FXレイヤーの第1と第2のレイヤーがジェスチャー様");
            public static readonly AnalysisResultIdentifier MayNotBeGestureLayers =
                AnalysisResultIdentifier.Create<Layer1And2Analyzer>("FXレイヤーの第1と第2のレイヤーがジェスチャー様ではない可能性があります");
        }
    }
}
