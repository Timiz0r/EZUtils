namespace EZUtils.Localization
{
    using System.Collections.Generic;
    using System.Linq;
    using EZUtils;

    public class UnityAssemblyRootResolver : IAssemblyRootResolver
    {
        private readonly IReadOnlyDictionary<string, string> assemblyNameToRoot;

        public UnityAssemblyRootResolver(IReadOnlyList<AssemblyDefinition> assemblyDefinitions)
        {
            assemblyNameToRoot = assemblyDefinitions
                .Where(ad => ad.Assembly != null)
                .ToDictionary(ad => ad.Assembly.GetName().Name, ad => ad.Root);
        }

        public string GetAssemblyRoot(string assemblyName)
            => assemblyNameToRoot.TryGetValue(assemblyName, out string root) ? root : null;
    }
}
