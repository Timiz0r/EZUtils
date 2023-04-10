namespace EZUtils
{
    using System;
    using System.Collections.Generic;

    public class DelayInitializer
    {
        private readonly List<Action> actions = new List<Action>();
        private bool initialized = false;

        public void Execute(Action action)
        {
            if (initialized)
            {
                action();
            }
            else
            {
                actions.Add(action);
            }
        }

        public void Initialize()
        {
            initialized = true;

            foreach (Action action in actions)
            {
                action();
            }

            actions.Clear();
            actions.Capacity = 0;
        }
    }
}
