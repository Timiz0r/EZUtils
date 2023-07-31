namespace EZUtils
{
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    public static class UIEditorPrefs
    {
        public static Toggle ForPref(this Toggle toggle, string prefName, bool defaultValue)
        {
            toggle.SetValueWithoutNotify(EditorPrefs.GetBool(prefName, defaultValue));
            _ = toggle.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(prefName, evt.newValue));
            return toggle;
        }

        public static Toggle ForPref(this Toggle toggle, EditorPreference<bool> editorPreference)
        {
            toggle.SetValueWithoutNotify(editorPreference.Value);
            _ = toggle.RegisterValueChangedCallback(evt => editorPreference.Value = evt.newValue);
            return toggle;
        }
        public static IntegerField ForPref(this IntegerField integerField, EditorPreference<int> editorPreference)
        {
            integerField.SetValueWithoutNotify(editorPreference.Value);
            _ = integerField.RegisterValueChangedCallback(evt => editorPreference.Value = evt.newValue);
            return integerField;
        }
    }
}
