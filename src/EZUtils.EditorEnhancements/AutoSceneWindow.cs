namespace EZUtils.EditorEnhancements
{
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public static class AutoSceneWindow
    {
        public static readonly string PrefName = "EZUtils.EditorEnhancements.AutoSceneWindow";

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (EditorPrefs.GetBool(PrefName, true) && s == PlayModeStateChange.EnteredPlayMode)
                {
                    FocusSceneViewIfNotUploading();
                }
            };
            EditorApplication.pauseStateChanged += s =>
            {
                if (EditorPrefs.GetBool(PrefName, true) && s == PauseState.Unpaused)
                {
                    //the unpaused event happens before the window switch, so we gotta wait a bit
                    EditorApplication.update += EditorUpdate;
                }
            };

            void EditorUpdate()
            {
                if (EditorWindow.focusedWindow?.titleContent?.text == "Game")
                {
                    FocusSceneViewIfNotUploading();
                    EditorApplication.update -= EditorUpdate;
                }
            }

            void FocusSceneViewIfNotUploading()
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene currentScene = SceneManager.GetSceneAt(i);
                    if (!currentScene.isLoaded) continue;
                    foreach (GameObject go in currentScene.GetRootGameObjects())
                    {
                        if (go.name == "VRCSDK")
                        {
                            return;
                        }
                    }
                }
                EditorWindow.FocusWindowIfItsOpen<SceneView>();
            }
        }
    }
}
