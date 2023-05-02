namespace EZUtils.Localization.UIElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine.UIElements;

    internal class LocaleSelectionUI
    {
        private static readonly List<LocaleSelectionUI> instances = new List<LocaleSelectionUI>();

        private readonly CatalogLocaleSynchronizer owningSynchronizer;

        public LocaleSelectionUI(CatalogLocaleSynchronizer owningSynchronizer)
        {
            this.owningSynchronizer = owningSynchronizer;
            RegenerateMenu();
            instances.Add(this);
        }

        public DropdownMenu DropdownMenu { get; } = new DropdownMenu();

        //NOTE: far from an ideal design to propagate this information this way, via static method
        //and performance isn't technically ideal, regenerating multiple menus where maybe only one needs changing
        //but it's a lot easier to write and likely not an issue in practice.
        //if necessary, perhaps we pass the catalogreference here, go thru each instance's synchronizer, and ask it if it's relevant
        //but definitely don't let catalogreference contain instances of synchronizers, an event, or whatever.
        public static void ReportChange() => instances.ForEach(i => i.RegenerateMenu());

        public void RegenerateMenu()
        {
            List<DropdownMenuItem> menuItems = DropdownMenu.MenuItems();
            menuItems.Clear();
            menuItems.AddRange(owningSynchronizer.SupportedLocales
                .Select(l => new DropdownMenuAction(
                    l.CultureInfo.NativeName,
                    a => owningSynchronizer.SelectLocale(l),
                    a => l == owningSynchronizer.SelectedLocale
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal)));
        }
    }
}
