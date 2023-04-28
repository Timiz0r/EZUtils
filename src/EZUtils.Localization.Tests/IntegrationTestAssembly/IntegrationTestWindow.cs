namespace EZUtils.Localization.Tests.Integration
{
    using UnityEditor;
    using UnityEngine.UIElements;

    public class IntegrationTestWindow : EditorWindow
    {
        public static IntegrationTestWindow Create(params VisualElement[] elements)
        {
            IntegrationTestWindow window = GetWindow<IntegrationTestWindow>();
            foreach (VisualElement element in elements)
            {
                window.rootVisualElement.Add(element);
            }
            return window;
        }
    }
}
