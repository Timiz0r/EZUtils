namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor.Animations;
    using VRC.SDK3.Avatars.Components;

    //still don't really know why empty states  cause issues, but they always seem to. hence, cant really go harder than warn.
    //a previous version looked at all custom controllers. we no longer do that because, theoretically,
    //only a misbehaving fx layer could cause mmd issues. well, that an the action layer, which will typically be
    //blended out. could include it, though, or all layers if we find an issue!
    public class EmptyStateAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            if (avatar.baseAnimationLayers == null)
            {
                return AnalysisResult.Create(
                    Result.FXLayerHasNoEmptyStates,
                    AnalysisResultLevel.Pass,
                    new GeneralRenderer("FXレイヤーが指定されていません。")
                );
            }

            //will consider it ub for there to be multiple
            AnimatorController fxController =
                (AnimatorController)avatar.baseAnimationLayers
                .FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX)
                .animatorController;
            if (fxController == null)
            {
                return AnalysisResult.Create(
                    Result.FXLayerHasNoEmptyStates,
                    AnalysisResultLevel.Pass,
                    new GeneralRenderer("FXレイヤーが指定されていません。")
                );
            }

            (string layerName, string stateName)[] statesWithEmptyMotions = fxController.layers
                .SelectMany(l => l.stateMachine.states
                    .Where(s => s.state.motion == null)
                    .Select(s => (
                        layerName: l.name,
                        states: s.state.name
                )))
                .ToArray();

            return statesWithEmptyMotions.Length == 0
                ? AnalysisResult.Create(
                    Result.FXLayerHasNoEmptyStates,
                    AnalysisResultLevel.Pass,
                    new EmptyRenderer())
                : AnalysisResult.Create(
                    Result.FXLayerHasEmptyStates,
                    AnalysisResultLevel.Warning,
                    new GeneralRenderer(
                        "異変が起こる可能性があります。",
                        new AnimatorStateRenderer(
                            "モーションのないステート",
                            fxController,
                            statesWithEmptyMotions)));
        }

        public static class Result
        {
            public static readonly AnalysisResultIdentifier FXLayerHasEmptyStates =
                AnalysisResultIdentifier.Create<EmptyStateAnalyzer>("FXレイヤーにはモーションのないステートがあります");
            public static readonly AnalysisResultIdentifier FXLayerHasNoEmptyStates =
                AnalysisResultIdentifier.Create<EmptyStateAnalyzer>("FXレイヤーの全ステートにはモーションがあります");
        }
    }
}
