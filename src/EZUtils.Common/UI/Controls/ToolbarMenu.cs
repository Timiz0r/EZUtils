
namespace EZUtils.UIElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    public class ToolbarMenu : TextElement, IToolbarMenuElement
    {
        private new class UxmlFactory : UxmlFactory<ToolbarMenu, UxmlTraits> { }
        private new class UxmlTraits : TextElement.UxmlTraits
        {
            public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription { get; }
                = Enumerable.Repeat(new UxmlChildElementDescription(typeof(VisualElement)), 1);

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) => base.Init(ve, bag, cc);
        }

        public ToolbarMenu()
        {
            this.AddManipulator(new Clickable(() => this.ShowMenu()));
            generateVisualContent = null;

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.timiz0r.ezutils.common/UI/Controls/ToolbarMenu.uss");
            styleSheets.Add(styleSheet);

#pragma warning disable CA2245 // The property text should not be assigned to itself
            //the text binding is done before child elements get added in uxml; quick workaround
            RegisterCallback<AttachToPanelEvent>(_ => text = text);
#pragma warning restore CA2245
        }

        public DropdownMenu menu { get; private set; } = new DropdownMenu();

        public override string text
        {
            get => base.text;
            set
            {
                base.text = value;

                _ = this
                    .Query<TextElement>(className: "toolbar-menu-label")
                    .ForEach(te => te.text = value);
            }
        }

        public void BindMenu(DropdownMenu dropdownMenu) => menu = dropdownMenu;
    }
}
