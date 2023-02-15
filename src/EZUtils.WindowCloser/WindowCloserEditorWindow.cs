namespace EZUtils.WindowCloser
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class WindowCloserEditorWindow : EditorWindow
    {
        [MenuItem("Window/Window Closer", isValidateFunction: false, priority: 0)]
        public static void ShowWindow()
        {
            WindowCloserEditorWindow window = GetWindow<WindowCloserEditorWindow>("Window closer");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.EZUtils.WindowCloser/WindowCloserEditorWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            //TODO: want to show a list of windows, but first want to create the common package that would
            //contain useful controls
            rootVisualElement.Q<Button>(name: "close").clicked += () =>
            {
                string targetWindowName = rootVisualElement.Q<TextField>(name: "windowName").value;

                EditorWindow[] openWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                foreach (EditorWindow window in openWindows)
                {
                    if (window.titleContent.text.Equals(targetWindowName, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            window.Close();
                        }
                        catch (Exception ex)
                        {
                            Debug.Log($"Failed to close window through normal means: {ex}");

                            try
                            {
                                DestroyImmediate(window, allowDestroyingAssets: true);
                            }
                            catch (Exception ex2)
                            {
                                Debug.Log($"Failed to destroy window: {ex2}");
                            }
                        }
                    }
                }
            };
        }
    }
}
