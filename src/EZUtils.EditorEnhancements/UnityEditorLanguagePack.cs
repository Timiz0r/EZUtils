namespace EZUtils.EditorEnhancements
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using UnityEditor;
    using UnityEngine;

    public class UnityEditorLanguagePack
    {
        private static readonly string unityPath = Path.Combine(EditorApplication.applicationPath, "../../");
        private static readonly HttpClient httpClient = new HttpClient();

        private readonly Uri downloadUrl;
        private readonly string filePath;
        private readonly string onDiskManifestPath;

        public string Name { get; }
        public bool IsInstalled { get; }

        public UnityEditorLanguagePack(
            string name,
            Uri downloadUrl,
            string destinationFolder,
            string onDiskManifestPath)
        {
            Name = name;
            this.downloadUrl = downloadUrl;
            this.onDiskManifestPath = onDiskManifestPath;
            filePath = Path.Combine(
                destinationFolder.Replace("{UNITY_PATH}", unityPath),
                downloadUrl.Segments.Last() + ".po");
            IsInstalled = File.Exists(filePath);
        }

        public async Task Install()
        {
            HttpResponseMessage response = await httpClient.GetAsync(downloadUrl);
            using (FileStream fs = new FileStream(filePath, FileMode.CreateNew))
            {
                await response.EnsureSuccessStatusCode().Content.CopyToAsync(fs);
            }

            if (!string.IsNullOrEmpty(onDiskManifestPath))
            {
                JArray manifest;
                using (StreamReader sr = new StreamReader(onDiskManifestPath))
                using (JsonTextReader jr = new JsonTextReader(sr))
                {
                    manifest = await JArray.LoadAsync(jr);

                    JToken module = manifest.Single(j => j["name"].Value<string>() == Name);
                    module["selected"] = true;
                }

                using (StreamWriter sw = new StreamWriter(onDiskManifestPath))
                using (JsonTextWriter jw = new JsonTextWriter(sw))
                {
                    await manifest.WriteToAsync(jw);
                }
            }
        }

        public static Task<IReadOnlyList<UnityEditorLanguagePack>> GetAvailable()
        {
            FileInfo moduleManifestFile = new FileInfo(Path.Combine(unityPath, "modules.json"));
            if (moduleManifestFile.Exists)
            {
                return Task.FromResult(GetLanguagePacksFromManifest(moduleManifestFile.FullName));
            }

            return GetLanguagePacksFromApi();
        }

        private static IReadOnlyList<UnityEditorLanguagePack> GetLanguagePacksFromManifest(
            string manifestPath)
        {
            using (StreamReader sr = new StreamReader(manifestPath))
            using (JsonTextReader jr = new JsonTextReader(sr))
            {
                JArray manifest = JArray.Load(jr);

                UnityEditorLanguagePack[] results = manifest
                    .Where(j => j["category"].Value<string>() == "Language packs (Preview)")
                    .Select(j => new UnityEditorLanguagePack(
                        j["name"].Value<string>(),
                        new Uri(j["downloadUrl"].Value<string>()),
                        j["destination"].Value<string>(),
                        onDiskManifestPath: manifestPath))
                    .ToArray();
                return results;
            }
        }

        private static async Task<IReadOnlyList<UnityEditorLanguagePack>> GetLanguagePacksFromApi()
        {
            JObject requestJson = new JObject()
                {
                    { "operationName", "getRelease" },
                    { "query", query },
                    {
                        "variables",
                        new JObject()
                        {
                            { "version", Application.unityVersion }
                        }
                    },
                };
            using (StringContent requestContent = new StringContent(requestJson.ToString(), Encoding.UTF8, "application/json"))
            {
                HttpResponseMessage response = await httpClient.PostAsync(
                    new Uri("https://live-platform-api.prd.ld.unity3d.com/graphql"), requestContent);
                _ = response.EnsureSuccessStatusCode();

                JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
                JArray modules = (JArray)content["data"]["getUnityReleases"]["edges"][0]["node"]["downloads"][0]["modules"];
                UnityEditorLanguagePack[] results = modules
                    .Where(j => j["type"].Value<string>() == "PO")
                    .Select(j => new UnityEditorLanguagePack(
                        j["name"].Value<string>(),
                        new Uri(j["url"].Value<string>()),
                        j["destination"].Value<string>(),
                        onDiskManifestPath: null
                    ))
                    .ToArray();
                return results;
            }
        }

        private const string query = @"
query getRelease($version: String) {
  getUnityReleases(
    version: $version,
    limit: 1,
    platform: WINDOWS #language packs are platform agnostic
  ) {
    edges {
      node {
        downloads {
          ... on UnityReleaseHubDownload {
            modules {
              url
              type
              id
              name
              description
              category
              destination
            }
          }
        }
      }
    }
  }
}
";
    }
}
