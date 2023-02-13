namespace EZUtils.RepackPrefab
{
    using System.Reflection;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    //kinda an experiment that will be moved to a more common area later
    //want to avoid exposing the underlying object if not too tedious
    //or just expose it
    public class TypedObjectField<T> : CallbackEventHandler, INotifyValueChanged<T> where T : Object
    {
        private readonly ObjectField underlyingObjectField;

        public TypedObjectField(ObjectField underlyingObjectField)
        {
            this.underlyingObjectField = underlyingObjectField;
            underlyingObjectField.objectType = typeof(T);

            _ = underlyingObjectField.RegisterValueChangedCallback(UnderlyingValueChanged);
        }

        public T value
        {
            get => (T)underlyingObjectField.value;
            set => underlyingObjectField.value = value;
        }

        public override void SendEvent(EventBase e) => throw new System.NotImplementedException();

        public void SetValueWithoutNotify(T newValue) => underlyingObjectField.SetValueWithoutNotify(newValue);

        //at least in unity 2019 (didnt check others), it's not really possible to repackage an event and send it off
        //the best that can be done there involves this class inheriting from CallbackEventHandler,
        //copying over a variety of fields (set via reflection; most importantly propagationPhase),
        //and calling CallbackEventHandler.HandleEvent. the main requirement is propagationPhase, but gotta account for
        //what handlers might want. this solution allows existing RegisterValueChangedCallback to work and theoretically
        //produces no surprises.
        //
        //we also dont get a way to hook into event registration, which could have allowed us to get in the middle.
        //
        //second/third option is to implement our own event stuff. this class should also not use
        //INotifyValueChanged because it'll go through an ineffective RegisterValueChangedCallback.
        //
        //not happy about using reflection, but it's somewhat hard to imagine a bug happening given the current
        //HandleEvent implementation (of course it's future unitys that are scary). the option of our own even stuff
        //also leaves us open to bugs in the future and violates the principal of least surprise often. if we use
        //ChangeEvent, then we have to do mapping anyway. if we don't, then users of value change events need two
        //conventions. and there's more pain i dont feel like writing down.
        private void UnderlyingValueChanged(ChangeEvent<Object> evt)
        {
            using (ChangeEvent<T> newEvent = ChangeEvent<T>.GetPooled((T)evt.previousValue, (T)evt.newValue))
            {
                foreach (FieldInfo fieldInfo in typeof(EventBase)
                    .GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    fieldInfo.SetValue(newEvent, fieldInfo.GetValue(evt));
                }
                HandleEvent(newEvent);
            }
        }
    }
}
