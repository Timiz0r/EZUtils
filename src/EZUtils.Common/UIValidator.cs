namespace EZUtils
{
    using System;
    using System.Collections.Generic;
    using UnityEngine.UIElements;

    public class UIValidator
    {
        private readonly Dictionary<object, ValidationIssue> validationIssues =
            new Dictionary<object, ValidationIssue>();
        private readonly List<VisualElement> elementsToDisable = new List<VisualElement>();

        public void AddValueValidation<T>(
            INotifyValueChanged<T> sourceObject, Func<T, bool> passCondition)
        {
            void callback(ChangeEvent<T> e)
            {
                if (passCondition(e.newValue))
                {
                    _ = validationIssues.Remove(sourceObject);
                }
                else
                {
                    validationIssues.Add(sourceObject, new ValidationIssue());
                }

                elementsToDisable.ForEach(element => element.SetEnabled(validationIssues.Count == 0));
            }
            _ = sourceObject.RegisterValueChangedCallback(callback);
            using (ChangeEvent<T> e = ChangeEvent<T>.GetPooled(default, sourceObject.value))
            {
                callback(e);
            }
        }

        public void DisableIfInvalid(VisualElement element)
        {
            elementsToDisable.Add(element);
            element.SetEnabled(validationIssues.Count == 0);
        }

        //likely to be expanded with diagnostic info, so might as well go with a class
        public class ValidationIssue
        {
        }
    }
}
