namespace EZUtils.Localization.UIElements
{
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    public static class LocalizationUIExtensions
    {
        public static void AddLocaleSelector(this Toolbar toolbar)
            => AddLocaleSelector(toolbar, "EZUtils", Locale.English);
        public static void AddLocaleSelector(
            this Toolbar toolbar, string localeSynchronizationKey, Locale initialLocale)
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.timiz0r.EZUtils.Localization/UI/LocaleSelectionMenu.uxml");
            EZUtils.UIElements.ToolbarMenu menu = visualTree.CloneTree().Q<EZUtils.UIElements.ToolbarMenu>();
            toolbar.Add(menu);

            CatalogLocaleSynchronizer synchronizer =
                CatalogLocaleSynchronizer.Get(localeSynchronizationKey, initialLocale);
            menu.BindMenu(synchronizer.UI.DropdownMenu);
        }
    }
}
