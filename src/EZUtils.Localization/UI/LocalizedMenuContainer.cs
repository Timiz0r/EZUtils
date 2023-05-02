namespace EZUtils.Localization //purposely not uielements, unlike many other types in this folder
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor;

    internal class LocalizedMenuContainer : IRetranslatable
    {
        //very tricky to get working, but referencing WindowLayout worked well.
        //we need to, in a delayCall or similar, finish off menu operations with an Internal_UpdateAllMenus call
        //we achieve the delayCall requirement by having EZLocalization initialize in a delayCall
        private static readonly MethodInfo addMenuMethod =
            typeof(Menu).GetMethod("AddMenuItem", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo removeMenuItemMethod =
            typeof(Menu).GetMethod("RemoveMenuItem", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo updateAllMenusMethod =
            typeof(EditorUtility).GetMethod("Internal_UpdateAllMenus", BindingFlags.NonPublic | BindingFlags.Static);

        private readonly EZLocalization loc;
        private readonly List<Descriptor> descriptors = new List<Descriptor>();
        private bool firstRetranslateOccurred = false;

        public bool IsFinished => false;

        public LocalizedMenuContainer(EZLocalization loc)
        {
            this.loc = loc;
        }

        public void AddMenu(string name, int priority, Action action)
        {
            Descriptor descriptor = new Descriptor(
                nativeName: name,
                shortcut: string.Empty,
                @checked: false,
                priority,
                execute: action,
                validate: null);
            descriptors.Add(descriptor);

            if (!firstRetranslateOccurred) return;

            AddMenu(descriptor);
            UpdateAllMenus();
        }

        public void Retranslate()
        {
            firstRetranslateOccurred = true;

            foreach (Descriptor descriptor in descriptors)
            {
                RemoveMenu(descriptor);
            }

            foreach (Descriptor descriptor in descriptors)
            {
                AddMenu(descriptor);
            }
            UpdateAllMenus();
        }

        private void AddMenu(Descriptor descriptor)
            => _ = addMenuMethod.Invoke(null, new object[]
            {
                descriptor.SetNewName(
                    loc.T(descriptor.NativeName)),
                descriptor.Shortcut,
                descriptor.Checked,
                descriptor.Priority,
                descriptor.Execute,
                descriptor.Validate
            });
        private static void RemoveMenu(Descriptor descriptor)
            => _ = removeMenuItemMethod.Invoke(null, new object[] { descriptor.PreviousName });
        private static void UpdateAllMenus() => _ = updateAllMenusMethod.Invoke(null, Array.Empty<object>());
        private class Descriptor
        {
            public string NativeName { get; }
            public string Shortcut { get; }
            public bool Checked { get; }
            public int Priority { get; }
            public Action Execute { get; }
            public Func<bool> Validate { get; }
            public string PreviousName { get; private set; }

            public Descriptor(string nativeName, string shortcut, bool @checked, int priority, Action execute, Func<bool> validate)
            {
                NativeName = nativeName;
                Shortcut = shortcut;
                Checked = @checked;
                Priority = priority;
                Execute = execute;
                Validate = validate;
                PreviousName = nativeName;
            }

            public string SetNewName(string name)
            {
                PreviousName = name;
                return name;
            }
        }
    }
}
