namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EZUtils.Localization;
    using EZUtils.Localization.UIElements;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VRC.SDK3.Avatars.Components;

    using static Localization;

    //TODO: translate refactor! consider crowdin scenario. dont anticipate needing zero, so just use default ja.
    //TODO: push to main but dont yet do version increase for this package
    //TODO: get crowdin up and running
    //TODO: translate
    public class MmdAvatarTesterEditorWindow : EditorWindow
    {
        private readonly UIValidator testerValidation = new UIValidator();
        private readonly UIValidator analysisValidation = new UIValidator();
        private readonly MmdAvatarTester mmdAvatarTester = new MmdAvatarTester();

        private TypedObjectField<VRCAvatarDescriptor> targetAvatar;
        private VisualTreeAsset analysisResultUxml;

        [InitializeOnLoadMethod]
        private static void UnityInitialize() => AddMenu("EZUtils/MMD avatar tester", priority: 0, CreateWindow);

        public static void CreateWindow()
        {
            MmdAvatarTesterEditorWindow window = GetWindow<MmdAvatarTesterEditorWindow>("MMD Tester");
            window.Show();
        }

        public void CreateGUI()
        {
            TranslateWindowTitle(this, "MMD Tester");

            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.EZUtils.MMDAvatarTools/MmdAvatarTesterEditorWindow.uxml");
            visualTree.CommonUIClone(rootVisualElement);
            TranslateElementTree(rootVisualElement);

            rootVisualElement.Q<Toolbar>().AddLocaleSelector();

            //not allowed to go in cctor, so here is as good as any other place
            if (analysisResultUxml == null)
            {
                analysisResultUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                    "Packages/com.timiz0r.ezutils.mmdavatartools/Analysis/AnalysisResultElement.uxml");
            }

            //probably dont need allowSceneObjects, but meh
            targetAvatar = rootVisualElement
                .Q<ObjectField>(name: "targetAvatar")
                .Typed<VRCAvatarDescriptor>(f => f.allowSceneObjects = true);

            RenderAvatarTester();
            RenderAvatarAnalyzer();
        }

        private void RenderAvatarTester()
        {
            TypedObjectField<AnimationClip> targetAnimation = rootVisualElement
                .Q<ObjectField>(name: "targetAnimation")
                .Typed<AnimationClip>(f => f.allowSceneObjects = false);

            //isPlayingOrWillChangePlaymode covers if the window was open before entering playmode
            rootVisualElement.EnableInClassList(
                "play-mode",
                EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying);
            Action<PlayModeStateChange> playModeStateChangedCallback = null;
            EditorApplication.playModeStateChanged += playModeStateChangedCallback = s =>
            {
                //this is only here because the ui doesnt get reset when exiting playmode
                if (s == PlayModeStateChange.ExitingPlayMode)
                {
                    rootVisualElement.RemoveFromClassList("play-mode");
                    Stop();
                    EditorApplication.playModeStateChanged -= playModeStateChangedCallback;
                }
            };

            Button startButton = rootVisualElement.Q<Button>(name: "start");
            startButton.clicked += () =>
            {
                mmdAvatarTester.Start(targetAvatar.value, targetAnimation.value);
                rootVisualElement.AddToClassList("running");
            };

            Button stopButton = rootVisualElement.Q<Button>(name: "stop");
            stopButton.clicked += () => Stop();

            targetAnimation.SetValueWithoutNotify(
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Packages/com.timiz0r.ezutils.mmdavatartools/mmdsample.anim"));

            testerValidation.AddValueValidation(targetAvatar, passCondition: o => o != null);
            testerValidation.AddValueValidation(targetAnimation, passCondition: o => o != null);
            testerValidation.DisableIfInvalid(startButton);
            testerValidation.TriggerWhenInvalid(() => Stop());

            void Stop()
            {
                mmdAvatarTester.Stop();
                rootVisualElement.RemoveFromClassList("running");
            }
        }

        private RetranslatableCommand avatarAnalysisResultCommand = null;
        private void RenderAvatarAnalyzer()
        {
            Button analyzeButton = rootVisualElement.Q<Button>(name: "analyze");
            ScrollView resultsContainer = rootVisualElement.Q<ScrollView>(className: "analyzer-result-container");

            analyzeButton.clicked += () => ValidateAvatar();

            analysisValidation.AddValueValidation(targetAvatar, passCondition: o => o != null);
            analysisValidation.DisableIfInvalid(analyzeButton);
            analysisValidation.TriggerWhenValid(() => ValidateAvatar());
            analysisValidation.TriggerWhenInvalid(() => resultsContainer.Clear());


            void ValidateAvatar()
            {
                avatarAnalysisResultCommand?.ForceFinished();

                avatarAnalysisResultCommand = new RetranslatableCommand(
                    finishedFunc: () => false, //we'll force it finished when the time is right
                    action: () =>
                    {
                        resultsContainer.Clear();

                        MmdAvatarAnalyzer analyzer = new MmdAvatarAnalyzer();
                        IReadOnlyList<AnalysisResult> results = analyzer.Analyze(targetAvatar.value);

                        foreach (AnalysisResult result in results.OrderByDescending(r => r.Level))
                        {

                            VisualElement resultElement = analysisResultUxml.CommonUIClone();
                            resultsContainer.Add(resultElement);

                            resultElement
                                .Q<VisualElement>(className: "analyzer-result-icon")
                                .AddToClassList($"analyzer-result-icon-{result.Level.ToString().ToLowerInvariant()}");
                            resultElement.Q<Label>(className: "analyzer-result-friendlyname").text =
                                result.Result.FriendlyName;
                            resultElement.Q<Label>(className: "analyzer-result-code").text = result.Result.Code;

                            result.Renderer?.Render(
                                resultElement.Q<VisualElement>(className: "analyzer-result-details"));
                        }
                    });
                TrackRetranslatable(avatarAnalysisResultCommand);
            };
        }
    }
}
