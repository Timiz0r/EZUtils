namespace EZUtils.Bootstrap.com_timiz0r_ezutils_ezfxlayer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEditor.PackageManager.Requests;
    using UPM = UnityEditor.PackageManager;

    public static class UPMPackageClient
    {
        private static IRequestDescriptor currentRequest = null;
        private static readonly ConcurrentQueue<IRequestDescriptor> requestQueue = new ConcurrentQueue<IRequestDescriptor>();

        public static async Task<IReadOnlyList<UPM.PackageInfo>> ListAsync(bool offlineMode)
        {
            Task<UPM.PackageCollection> request = Request(() => UPM.Client.List(offlineMode));
            UPM.PackageCollection result = await request;
            return result.ToArray();
        }

        public static Task<UPM.PackageInfo> AddAsync(string identifier) => Request(() => UPM.Client.Add(identifier));

        public static Task RemoveAsync(string name) => Request(() => UPM.Client.Remove(name));

        private static Task<T> Request<T>(Func<Request<T>> requestCreator)
            => RequestDescriptor<T>.Create(requestCreator).Task;
        private static Task Request(Func<Request> requestCreator)
            => RequestDescriptor.Create(requestCreator).Task;

        static UPMPackageClient()
        {
            EditorApplication.update += () =>
            {
                if (currentRequest == null)
                {
                    if (!requestQueue.TryDequeue(out currentRequest)) return;
                    currentRequest.Begin();
                }

                if (currentRequest.Task.IsCompleted)
                {
                    currentRequest = null;
                }
                else
                {
                    currentRequest.Poll();
                }
            };
        }

        private interface IRequestDescriptor
        {
            void Begin();
            void Poll();
            Task Task { get; }
        }

        private class RequestDescriptor : IRequestDescriptor
        {
            private readonly TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            private readonly Func<Request> requestStarter;
            private Request request;

            private RequestDescriptor(Func<Request> requestStarter)
            {
                this.requestStarter = requestStarter;
            }

            public static RequestDescriptor Create(Func<Request> requestStarter)
            {
                RequestDescriptor newDescriptor = new RequestDescriptor(requestStarter);
                requestQueue.Enqueue(newDescriptor);
                return newDescriptor;
            }

            public Task Task => tcs.Task;

            public void Begin() => request = requestStarter();
            public void Poll()
            {
                if (request.Error != null)
                {
                    tcs.SetException(new InvalidOperationException(request.Error.message));
                }
                else if (request.IsCompleted)
                {
                    tcs.SetResult(new object());
                }
            }
        }

        private class RequestDescriptor<T> : IRequestDescriptor
        {
            private readonly TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
            private readonly Func<Request<T>> requestStarter;
            private Request<T> request;

            private RequestDescriptor(Func<Request<T>> requestStarter)
            {
                this.requestStarter = requestStarter;
            }

            public Task<T> Task => tcs.Task;

            Task IRequestDescriptor.Task => tcs.Task;

            public static RequestDescriptor<T> Create(Func<Request<T>> requestStarter)
            {
                RequestDescriptor<T> newDescriptor = new RequestDescriptor<T>(requestStarter);
                requestQueue.Enqueue(newDescriptor);
                return newDescriptor;
            }

            public void Begin() => request = requestStarter();

            public void Poll()
            {
                if (request.Error != null)
                {
                    tcs.SetException(new InvalidOperationException(request.Error.message));
                }
                else if (request.IsCompleted)
                {
                    tcs.SetResult(request.Result);
                }
            }
        }
    }
}
