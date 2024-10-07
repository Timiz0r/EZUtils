namespace EZUtils.UIElements
{
    using System;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public class SettingsProviderContainer : VisualElement
    {
        private new class UxmlFactory : UxmlFactory<SettingsProviderContainer, UxmlTraits> { }

        private new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription settingsPathAttribute = new UxmlStringAttributeDescription
            {
                name = "settingsPath",
            };
            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);

                string settingsPath = settingsPathAttribute.GetValueFromBag(bag, cc);
                SettingsProvider settingsProvider = GetSettingsProviders()
                    .SingleOrDefault(sp => sp.settingsPath.Equals(settingsPath, StringComparison.OrdinalIgnoreCase));

                if (settingsProvider != null)
                {
                    ve.Add(new Label($"Settings path '{settingsPath}' does not exist."));
                    Debug.LogError($"Unable to loead SettingsProviderContainer: Settings path '{settingsPath}' does not exist.");
                    return;
                }

                settingsProvider.OnActivate(string.Empty, ve);
                ve.Add(new IMGUIContainer(() =>
                {
                    try
                    {
                        settingsProvider.guiHandler(string.Empty);
                    }
                    catch (NullReferenceException)
                    {
                        //is because a settings window isn't set. happens in an innocuous place, so wont be an issue.
                        //CreateInstanceing a window doesn't work as well, since it'll get added to the layout.
                        //and dont really want to show a window that doesnt need to be shown
                        //is fixed in later unities tho
                    }
                }));
            }
        }

        private static SettingsProvider[] GetSettingsProviders()
        {
            SettingsProvider[] settingsProviders = (SettingsProvider[])typeof(SettingsService)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(m => m.Name == "FetchSettingsProviders" && m.GetParameters().Length == 0)
                .Single()
                .Invoke(null, Array.Empty<object>());

            return settingsProviders;
        }
    }
}
