namespace EZUtils.MMDAvatarTools
{
    using System.Collections.Generic;
    using System.Linq;
    using VRC.SDK3.Avatars.Components;

    //still don't really know why empty states  cause issues, but they always seem to. hence, cant really go harder than warn.
    //a previous version looked at all custom controllers. we no longer do that because, theoretically,
    //only a misbehaving fx layer could cause mmd issues. well, that an the action layer, which will typically be
    //blended out. could include it, though, or all layers if we find an issue!
    public class EmptyStateAnalyzer : IAnalyzer
    {
        public IReadOnlyList<AnalysisResult> Analyze(VRCAvatarDescriptor avatar)
        {
            PlayableLayerInformation playableLayerInformation = PlayableLayerInformation.Generate(avatar);
            if (playableLayerInformation.FX.UnderlyingController == null)
            {
                return AnalysisResult.Create(
                    Result.FXLayerHasNoEmptyStates,
                    AnalysisResultLevel.Pass,
                    new GeneralRenderer("FXレイヤーが指定されていません。")
                );
            }

            (string layerName, string stateName)[] statesWithEmptyMotions = playableLayerInformation.FX.States
                .Where(s =>
                    s.LayerIndex != 1
                    && s.LayerIndex != 2
                    && !s.IsAlwaysDisabled
                    && s.UnderlyingState.motion == null)
                .Select(s => (layerName: s.LayerName, states: s.StateName))
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
                            title: "モーションのないステート",
                            emptyMessage: "モーションのないステートがFXレイヤーに存在しません。",
                            animatorController: playableLayerInformation.FX.UnderlyingController,
                            states: statesWithEmptyMotions)));
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
