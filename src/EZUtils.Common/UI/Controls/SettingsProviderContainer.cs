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
                    .SingleOrDefault(sp => sp.settingsPath.Equals(settingsPath, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ArgumentOutOfRangeException(
                            "settingsPath", $"Settings path '{settingsPath}' does not exist.");

                settingsProvider.OnActivate(string.Empty, ve);
                //TODO: when settings are changed this way, an NRE will be logged because internal settingsProvider
                //can be null. this isn't a problem in future unitys tho.
                //there's also a bug where languages can't seem to be completely switched around. in my testing,
                //stuff gets stuck in japanese even when switching back to english.
                //if we can fix this ui bug, then we can also do our own UI to call the APIs just right.
                //if we can't fix this bug, then also live with the error. it's not like we're switching around
                //that often at all.
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
                .GetMethod("FetchSettingsProviders", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, Array.Empty<object>());

            return settingsProviders;
        }
    }
}
