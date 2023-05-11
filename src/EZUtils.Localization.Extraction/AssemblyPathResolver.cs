namespace EZUtils.Localization
{
    using Microsoft.CodeAnalysis;

    public class AssemblyPathResolver
    {
        private readonly IAssemblyRootResolver assemblyRootResolver;

        public string AssemblyFullName { get; }
        public string AssemblyRoot { get; }

        public AssemblyPathResolver(string assemblyFullName, string assemblyRoot, IAssemblyRootResolver assemblyRootResolver)
        {
            AssemblyFullName = assemblyFullName;
            AssemblyRoot = assemblyRoot;
            this.assemblyRootResolver = assemblyRootResolver;
        }

        public string Resolve(string assemblyFullName) => assemblyRootResolver.GetAssemblyRoot(assemblyFullName);
    }
}
