namespace EZUtils.Localization.UIElements
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine.UIElements;

    internal class LocaleSelectionUI
    {
        private readonly CatalogLocaleSynchronizer owningSynchronizer;

        public LocaleSelectionUI(CatalogLocaleSynchronizer owningSynchronizer)
        {
            this.owningSynchronizer = owningSynchronizer;
            ReportChange();
        }

        public DropdownMenu DropdownMenu { get; } = new DropdownMenu();

        public void ReportChange()
        {
            List<DropdownMenuItem> menuItems = DropdownMenu.MenuItems();
            menuItems.Clear();
            menuItems.AddRange(owningSynchronizer.SupportedLocales
                .Select(l => new DropdownMenuAction(
                    l.CultureInfo.NativeName,
                    a => owningSynchronizer.SelectLocale(l),
                    a => l == owningSynchronizer.SelectedLocale
                        ? DropdownMenuAction.Status.Checked | DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal)));
        }
    }
}
