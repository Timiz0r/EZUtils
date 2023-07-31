namespace EZUtils.UIElements
{
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    public static class FieldLimit
    {
        public static IntegerField Limit(this IntegerField integerField, int lowerBound = int.MinValue, int upperBound = int.MaxValue)
        {
            _ = integerField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue < lowerBound || evt.newValue > upperBound)
                {
                    evt.StopImmediatePropagation();
                    integerField.SetValueWithoutNotify(evt.previousValue);
                }
            });
            return integerField;
        }
    }
}
