namespace EZUtils.Localization
{
    using System;
    using System.Threading.Tasks.Dataflow;

    public interface IGetTextExtractionWorkRunner
    {
        void StartWork(Action action);
        void FinishWork();
    }

    public static class GetTextExtractionWorkRunner
    {
        public static IGetTextExtractionWorkRunner Create() => new TplDataflow();
        public static IGetTextExtractionWorkRunner CreateSynchronous() => new Synchronous();

        private class TplDataflow : IGetTextExtractionWorkRunner
        {
            private readonly ActionBlock<Action> queue = new ActionBlock<Action>(a => a(), new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            });

            public void StartWork(Action action) => _ = queue.Post(action);
            public void FinishWork()
            {
                queue.Complete();
                queue.Completion.Wait();
            }
        }

        private class Synchronous : IGetTextExtractionWorkRunner
        {
            public void StartWork(Action action) => action();
            public void FinishWork() { }
        }
    }
}
