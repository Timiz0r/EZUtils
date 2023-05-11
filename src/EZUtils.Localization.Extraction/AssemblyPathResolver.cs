namespace EZUtils.Localization
{
    public class AssemblyPathResolver
    {
        private readonly IAssemblyRootResolver assemblyRootResolver;

        public string AssemblyName { get; }
        public string AssemblyRoot { get; }

        public AssemblyPathResolver(string assemblyName, string assemblyRoot, IAssemblyRootResolver assemblyRootResolver)
        {
            AssemblyName = assemblyName;
            AssemblyRoot = assemblyRoot;
            this.assemblyRootResolver = assemblyRootResolver;
        }

        public string Resolve(string assemblyName) => assemblyRootResolver.GetAssemblyRoot(assemblyName);
    }
}
