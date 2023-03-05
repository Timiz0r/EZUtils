namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VRC.SDK3.Avatars.Components;

    public class MmdAvatarTesterEditorWindow : EditorWindow
    {
        private readonly UIValidator testerValidation = new UIValidator();
        private readonly UIValidator analysisValidation = new UIValidator();

        private TypedObjectField<VRCAvatarDescriptor> targetAvatar;
        private MmdAvatarTester mmdAvatarTester;
        private VisualTreeAsset analysisResultUxml;

        [MenuItem("EZUtils/MMD avatar tester", isValidateFunction: false, priority: 0)]
        public static void PackageManager()
        {
            MmdAvatarTesterEditorWindow window = GetWindow<MmdAvatarTesterEditorWindow>("MMD Tester");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.EZUtils.MMDAvatarTools/MmdAvatarTesterEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

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
                mmdAvatarTester = targetAvatar.value.gameObject.AddComponent<MmdAvatarTester>();
                mmdAvatarTester.StartTesting(targetAnimation.value);
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
                DestroyImmediate(mmdAvatarTester);
                rootVisualElement.RemoveFromClassList("running");
            }
        }

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
                MmdAvatarAnalyzer analyzer = new MmdAvatarAnalyzer();
                IReadOnlyList<AnalysisResult> results = analyzer.Analyze(targetAvatar.value);

                resultsContainer.Clear();
                foreach (AnalysisResult result in results.OrderByDescending(r => r.Level))
                {
                    VisualElement resultElement = analysisResultUxml.CloneTree();
                    resultsContainer.Add(resultElement);

                    resultElement
                        .Q<VisualElement>(className: "analyzer-result-icon")
                        .AddToClassList($"analyzer-result-icon-{result.Level.ToString().ToLowerInvariant()}");
                    resultElement.Q<Label>(className: "analyzer-result-friendlyname").text = result.Result.FriendlyName;
                    resultElement.Q<Label>(className: "analyzer-result-code").text = result.Result.Code;

                    if (result.Renderer != null)
                    {
                        result.Renderer.Render(resultElement.Q<VisualElement>(className: "analyzer-result-details"));
                    }
                }
            };
        }
    }
}
