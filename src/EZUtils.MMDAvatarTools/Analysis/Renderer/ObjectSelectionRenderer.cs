namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    public class ObjectSelectionRenderer : IAnalysisResultRenderer
    {
        private readonly string title;
        private readonly Type objectType;
        private readonly IReadOnlyList<UnityEngine.Object> objects;

        public ObjectSelectionRenderer(string title, Type objectType, IReadOnlyList<UnityEngine.Object> objects)
        {
            this.title = title;
            this.objectType = objectType;
            this.objects = objects;
        }

        public static ObjectSelectionRenderer Create<T>(string listTitle, IReadOnlyList<T> objects) where T : UnityEngine.Object
            => new ObjectSelectionRenderer(
                title: listTitle,
                objectType: typeof(T),
                objects: objects);

        public void Render(VisualElement container)
        {
            container.Add(new Label(title));

            VisualElement objectContainer = new VisualElement();
            // objectContainer.AddToClassList("result-objects");
            container.Add(objectContainer);

            foreach (UnityEngine.Object obj in objects)
            {
                ObjectField objectField = new ObjectField
                {
                    value = obj,
                    objectType = objectType
                };
                objectField.SetEnabled(false);
                objectContainer.Add(objectField);
            }
        }
    }
}
