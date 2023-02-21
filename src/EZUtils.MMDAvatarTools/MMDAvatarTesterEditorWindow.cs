namespace EZUtils.MMDAvatarTools
{
    using System;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VRC.SDK3.Avatars.Components;

    public class MmdAvatarTesterEditorWindow : EditorWindow
    {
        private bool validAvatarIsTargeted = false;
        private bool animatorControllerIsTargeted = false;
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
