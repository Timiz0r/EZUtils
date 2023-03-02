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
        private readonly List<Action> validActions = new List<Action>();
        private readonly List<Action> invalidActions = new List<Action>();

        public void AddValueValidation<T>(
            INotifyValueChanged<T> sourceObject, Func<T, bool> passCondition)
        {
            _ = sourceObject.RegisterValueChangedCallback(callback);
            using (ChangeEvent<T> e = ChangeEvent<T>.GetPooled(default, sourceObject.value))
            {
                callback(e);
            }

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
                if (validationIssues.Count == 0)
                {
                    validActions.ForEach(a => a());
                }
                else
                {
                    invalidActions.ForEach(a => a());
                }
            }
        }

        public void DisableIfInvalid(VisualElement element)
        {
            elementsToDisable.Add(element);
            element.SetEnabled(validationIssues.Count == 0);
        }

        public void TriggerWhenValid(Action action)
        {
            validActions.Add(action);
            if (validationIssues.Count == 0) action();
        }

        public void TriggerWhenInvalid(Action action)
        {
            invalidActions.Add(action);
            if (validationIssues.Count > 0) action();
        }

        //likely to be expanded with diagnostic info, so might as well go with a class
        public class ValidationIssue
        {
        }
    }
}
