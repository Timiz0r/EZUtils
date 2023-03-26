namespace EZUtils
{
    using UnityEditor;
    using UnityEngine.UIElements;

    public static class UIEditorPrefs
    {
        public static Toggle ForPref(this Toggle toggle, string prefName, bool defaultValue)
        {
            toggle.SetValueWithoutNotify(EditorPrefs.GetBool(prefName, defaultValue));
            _ = toggle.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(prefName, evt.newValue));
            return toggle;
        }
    }
}
