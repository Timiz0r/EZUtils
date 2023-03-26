namespace EZUtils.MMDAvatarTools
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    public class ObjectSelectionRenderer : IAnalysisResultRenderer
    {
        private readonly string title;
        private readonly string emptyMessage;
        private readonly Type objectType;
        private readonly IReadOnlyList<UnityEngine.Object> objects;

        public ObjectSelectionRenderer(
            string title,
            string emptyMessage,
            Type objectType,
            IReadOnlyList<UnityEngine.Object> objects)
        {
            this.title = title;
            this.emptyMessage = emptyMessage;
            this.objectType = objectType;
            this.objects = objects;
        }

        public static ObjectSelectionRenderer Create<T>(
            string listTitle,
            string emptyMessage,
            IReadOnlyList<T> objects) where T : UnityEngine.Object
            => new ObjectSelectionRenderer(
                title: listTitle,
                emptyMessage: emptyMessage,
                objectType: typeof(T),
                objects: objects);

        public void Render(VisualElement container)
        {
            container.Add(new Label(title).WithClasses("results-details-title"));

            if (objects.Count == 0)
            {
                container.Add(new Label(emptyMessage).WithClasses("result-details-emptylist"));
                return;
            }

            VisualElement objectContainer = new VisualElement();
            container.Add(objectContainer);

            foreach (UnityEngine.Object obj in objects)
            {
                //un uielements, a disabled element cannot be clicked
                //so whipped open the underlying objectfield code and recreated the handling, done via a parent element
                VisualElement objectFieldContainer = new VisualElement();
                objectContainer.Add(objectFieldContainer);
                objectFieldContainer.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.clickCount == 1)
                    {
                        EditorGUIUtility.PingObject(obj);
                        evt.StopPropagation();
                    }
                    else if (evt.clickCount == 2)
                    {
                        _ = AssetDatabase.OpenAsset(obj);
                        evt.StopPropagation();
                    }
                });

                ObjectField objectField = new ObjectField
                {
                    value = obj,
                    objectType = objectType
                };
                objectField.SetEnabled(false);
                objectFieldContainer.Add(objectField);
            }
        }
    }
}
