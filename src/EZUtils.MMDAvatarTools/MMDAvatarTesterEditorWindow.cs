namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VRC.SDK3.Avatars.Components;

    public class MmdAvatarTesterEditorWindow : EditorWindow
    {
        private VisualTreeAsset analysisResultUxml;
        private bool validAvatarIsTargeted = false;
        private bool animatorControllerIsTargeted = true;
        private readonly MmdAvatarTester mmdAvatarTester = new MmdAvatarTester();

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

            analysisResultUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.ezutils.mmdavatartools/Analysis/AnalysisResultElement.uxml");

            //probably dont need allowSceneObjects, but meh
            ObjectField targetAvatar = rootVisualElement.Q<ObjectField>(name: "targetAvatar");
            targetAvatar.objectType = typeof(VRCAvatarDescriptor);
            targetAvatar.allowSceneObjects = true;
            ObjectField targetAnimation = rootVisualElement.Q<ObjectField>(name: "targetAnimation");
            targetAnimation.objectType = typeof(AnimationClip);
            targetAnimation.allowSceneObjects = false;

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
                mmdAvatarTester.Start(
                    (VRCAvatarDescriptor)targetAvatar.value,
                    (AnimationClip)targetAnimation.value
                );
                rootVisualElement.AddToClassList("running");
            };
            EnableRunningIfPossible();

            Button stopButton = rootVisualElement.Q<Button>(name: "stop");
            stopButton.clicked += () => Stop();

            _ = targetAvatar.RegisterValueChangedCallback(_ =>
            {
                validAvatarIsTargeted = targetAvatar.value != null;
                EnableRunningIfPossible();
            });

            _ = targetAnimation.RegisterValueChangedCallback(_ =>
            {
                animatorControllerIsTargeted = targetAnimation.value != null;
                EnableRunningIfPossible();
            });
            targetAnimation.SetValueWithoutNotify(
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Packages/com.timiz0r.ezutils.mmdavatartools/mmdsample.anim"));

            rootVisualElement.Q<Button>(name: "analyze").clicked += () =>
            {
                MmdAvatarAnalyzer analyzer = new MmdAvatarAnalyzer();
                IReadOnlyList<AnalysisResult> results = analyzer.Analyze((VRCAvatarDescriptor)targetAvatar.value);

                ScrollView resultsContainer = rootVisualElement.Q<ScrollView>(className: "result-container");
                resultsContainer.Clear();

                foreach (AnalysisResult result in results)
                {
                    VisualElement resultElement = analysisResultUxml.CloneTree();
                    resultsContainer.Add(resultElement);

                    resultElement
                        .Q<VisualElement>(className: "result-icon")
                        .AddToClassList($"result-icon-{result.Level.ToString().ToLowerInvariant()}");
                    resultElement.Q<Label>(className: "result-friendlyname").text = result.Result.FriendlyName;
                    resultElement.Q<Label>(className: "result-code").text = result.Result.Code;

                    if (result.Renderer != null)
                    {
                        result.Renderer.Render(resultElement.Q<VisualElement>(className: "result-details"));
                    }
                }
            };


            void EnableRunningIfPossible()
                => startButton.SetEnabled(validAvatarIsTargeted && animatorControllerIsTargeted);

            void Stop()
            {
                mmdAvatarTester.Stop();
                rootVisualElement.RemoveFromClassList("running");
            }
        }
    }
}
