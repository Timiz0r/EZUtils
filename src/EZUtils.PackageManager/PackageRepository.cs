namespace EZUtils.PackageManager
{
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
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
                new Uri("https://feeds.dev.azure.com/timiz0r/EZUtils/_apis/packaging/Feeds/EZUtils/packages?protocolType=npm&packageNameQuery=com.timiz0r.ezutils&includeAllVersions=true"));
            if (!request.IsSuccessStatusCode) throw new InvalidOperationException(
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
                PackageVersion currentVersion = await GetCurrentlyUsedVersionAsync(versions, name);

                PackageInformation result = new PackageInformation(name, currentVersion, versions);
                results.Add(result);
            }

            return results;
        }

        public Task SetVersionAsync(PackageInformation packageInformation, PackageVersion targetVersion)
            => UPMPackageClient.AddAsync($"{packageInformation.Name}@{targetVersion.FullVersion}");

        public Task RemoveAsync(PackageInformation packageInformation)
            => UPMPackageClient.RemoveAsync(packageInformation.Name);

        public void CheckForScopedRegistry()
        {
            JObject manifest = JObject.Parse(File.ReadAllText(@"Packages\manifest.json"));
            JArray scopedRegistries = (JArray)manifest["scopedRegistries"];
            if (scopedRegistries == null)
            {
                manifest.Add("scopedRegistries", scopedRegistries = new JArray());
            }

            JObject scopedRegistry = scopedRegistries
                .Cast<JObject>()
                .SingleOrDefault(r => ((string)r["name"]) == "EZUtils");
            if (scopedRegistry == null)
            {
                scopedRegistries.Add(scopedRegistry = new JObject());
            }

            scopedRegistry["name"] = "EZUtils";
            scopedRegistry["url"] = "https://pkgs.dev.azure.com/timiz0r/EZUtils/_packaging/EZUtils/npm/registry";
            scopedRegistry["scopes"] = new JArray() { "com.timiz0r.ezutils" };

            File.WriteAllText(@"Packages\manifest.json", manifest.ToString());
        }

        private static async Task<PackageVersion> GetCurrentlyUsedVersionAsync(
            IEnumerable<PackageVersion> versions, string packageName)
        {
            IReadOnlyList<UPM.PackageInfo> packages = await UPMPackageClient.ListAsync(offlineMode: true);
            UPM.PackageInfo targetPackage = packages.FirstOrDefault(p => p.name == packageName);

            PackageVersion result = targetPackage == null
                ? null
                : PackageVersion.TryParse(targetPackage.version, out PackageVersion parsedVersion)
                    ? versions.Single(v => v == parsedVersion)
                    //we're effectively saying that if we have an unknown version, we'll consider it not installed
                    //installing will provide a known version, as well! 
                    : null;
            return result;
        }
    }

    public static class UPMPackageClient
    {
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
