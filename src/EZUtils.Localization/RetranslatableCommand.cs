namespace EZUtils.Localization
{
    using System;

    public class RetranslatableCommand : IRetranslatable
    {
        private readonly Func<bool> finishedFunc;
        private readonly Action action;
        private bool forceFinished = false;

        public RetranslatableCommand(Func<bool> finishedFunc, Action action)
        {
            this.finishedFunc = finishedFunc;
            this.action = action;
        }

        public bool IsFinished => forceFinished || finishedFunc();

        public void Retranslate() => action();

        public void ForceFinished() => forceFinished = true;
    }
}
