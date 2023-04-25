namespace EZUtils.Localization.UIElements
{
    using UnityEngine.UIElements;

    public class GenerateLanguage : VisualElement
    {
        private new class UxmlFactory : UxmlFactory<GenerateLanguage, UxmlTraits> { }

        private new class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription poFilePathAttribute = new UxmlStringAttributeDescription
            {
                name = "poFilePath",
            };
            private readonly UxmlStringAttributeDescription cultureInfoCodeAttribute = new UxmlStringAttributeDescription
            {
                name = "cultureInfoCode",
            };
            private readonly UxmlStringAttributeDescription zeroAttribute = new UxmlStringAttributeDescription
            {
                name = "zero",
            };
            private readonly UxmlStringAttributeDescription oneAttribute = new UxmlStringAttributeDescription
            {
                name = "one",
            };
            private readonly UxmlStringAttributeDescription twoAttribute = new UxmlStringAttributeDescription
            {
                name = "two",
            };
            private readonly UxmlStringAttributeDescription fewAttribute = new UxmlStringAttributeDescription
            {
                name = "few",
            };
            private readonly UxmlStringAttributeDescription manyAttribute = new UxmlStringAttributeDescription
            {
                name = "many",
            };
            private readonly UxmlStringAttributeDescription otherAttribute = new UxmlStringAttributeDescription
            {
                name = "other",
            };
            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) => base.Init(ve, bag, cc);
        }
    }
}
