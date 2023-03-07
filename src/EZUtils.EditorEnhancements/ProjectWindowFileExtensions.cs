namespace EZUtils.EditorEnhancements
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public static class ProjectWindowFileExtensions
    {
        public static readonly string PrefName = "EZUtils.EditorEnhancements.ProjectWindowFileExtensions";
        //https://github.com/Unity-Technologies/UnityCsReference/blob/2019.4/Editor/Mono/GUI/TreeView/TreeViewGUI.cs
        private const float LabelShift =
            16f //icon
            + 2f //gap between icon and file name label
            + 0f; //magic constant not currently needed
        //fyi `EditorStyles.label` does not appear accessible thru cctor, hence lazy (or creating each time)
        //would have been nice if we didnt need to adjust the style
        //either the style we get isn't exactly what's used in the underlying treeview, or we're doing something not quite right
        private static readonly Lazy<GUIStyle> labelStyle = new Lazy<GUIStyle>(() => new GUIStyle("TV Line")
        {
            alignment = TextAnchor.MiddleLeft,
            //in testing, for some reason, there's sometimes a left padding of 1 and sometimes not
            //not sure if it's some hidden behavior or i somehow mutated it on my machine (should be immutable)
            //setting it to 1 from the start should fix that
            padding = new RectOffset(left: 1, right: 1, top: 0, bottom: 0)
        });

        [InitializeOnLoadMethod]
        public static void Initialize() => EditorApplication.projectWindowItemOnGUI += ForProjectWindowItem;

        private static void ForProjectWindowItem(string guid, Rect selectionRect)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (!EditorPrefs.GetBool(PrefName, true)) return;

            //we wont support the larger views because, most often, the file name gets truncated anyway
            if (selectionRect.height > 16) return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || Directory.Exists(path)) return;

            FileInfo file = new FileInfo(path);
            if (string.IsNullOrEmpty(file.Extension)) return;

            //while this technically works, projectWindowItemOnGUI only gets called when the mouse click is released,
            //while the selection effect changes the gui when initially pressed. this means there's delay, which looks
            //worse than an inconsistent color
            //bool isFocused = Array.IndexOf(Selection.assetGUIDs, guid) >= 0;
            bool isFocused = false;

            Vector2 predictedFileNameSize = labelStyle.Value.CalcSize(new GUIContent(Path.GetFileNameWithoutExtension(file.Name)));
            Rect labelRect = selectionRect;
            labelRect.x += predictedFileNameSize.x + LabelShift;
            labelStyle.Value.Draw(labelRect, file.Extension, isHover: false, isActive: false, on: true, hasKeyboardFocus: isFocused);
        }
    }
}
