namespace EZUtils.PackageManager
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEditor.PackageManager.Requests;
    using UPM = UnityEditor.PackageManager;

    public class PackageRepository
    {
        private static readonly HttpClient httpClient = new HttpClient();
        public async Task<IReadOnlyList<PackageInformation>> ListAsync()
        {
            HttpResponseMessage request = await httpClient.GetAsync(
                "https://feeds.dev.azure.com/timiz0r/EZUtils/_apis/packaging/Feeds/EZUtils/packages?protocolType=npm&packageNameQuery=com.timiz0r.ezutils&includeAllVersions=true");
            if (!request.IsSuccessStatusCode) throw new System.Exception(
                $"Failed to list packages: {request.StatusCode} {request.ReasonPhrase}");

            JObject content = JObject.Parse(await request.Content.ReadAsStringAsync());

            List<PackageInformation> results = new List<PackageInformation>();
            foreach (JObject package in content["value"].Values<JObject>())
            {
                string name = (string)package["name"];
                PackageVersion[] versions = package["versions"]
                    .Select(v => PackageVersion.Parse((string)v["version"]))
                    .OrderByDescending(v => v)
                    .ToArray();
                PackageVersion currentVersion = await GetCurrentlyUsedVersionAsync(name);

                PackageInformation result = new PackageInformation(name, currentVersion, versions);
                results.Add(result);
            }

            return results;
        }

        internal Task SetVersionAsync(PackageInformation packageInformation, PackageVersion value) => throw new NotImplementedException();

        private static async Task<PackageVersion> GetCurrentlyUsedVersionAsync(string packageName)
        {
            IReadOnlyList<UPM.PackageInfo> packages = await UPMPackageClient.ListAsync(offlineMode: true);
            UPM.PackageInfo targetPackage = packages.FirstOrDefault(p => p.name == packageName);

            PackageVersion result = targetPackage == null
                ? null
                : PackageVersion.TryParse(targetPackage.version, out PackageVersion v)
                    ? v
                    //we're effectively saying that if we have an unknown version, we'll consider it not installed
                    //installing will provide a known version, as well!
                    : null;
            return result;
        }

        internal Task RemoveAsync(PackageInformation packageInformation) => throw new NotImplementedException();
    }

    public static class UPMPackageClient
    {
        private static IRequestDescriptor currentRequest = null;
        private static readonly ConcurrentQueue<IRequestDescriptor> requestQueue = new ConcurrentQueue<IRequestDescriptor>();
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

        public static async Task<IReadOnlyList<UPM.PackageInfo>> ListAsync(bool offlineMode)
        {
            RequestDescriptor<UPM.PackageCollection> requestDescriptor =
                RequestDescriptor<UPM.PackageCollection>.Create(() => UPM.Client.List(offlineMode));
            UPM.PackageCollection result = await requestDescriptor.Task;
            return result.ToArray();
        }

        private interface IRequestDescriptor
        {
            void Begin();
            void Poll();
            Task Task { get; }
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

            public void Begin()
            {
                request = requestStarter();
            }

            public void Poll()
            {
                if (request.Error != null) throw new InvalidOperationException(request.Error.message);
                if (request.IsCompleted)
                {
                    tcs.SetResult(request.Result);
                }
            }
        }
    }
}
