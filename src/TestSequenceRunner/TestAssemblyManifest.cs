using Microsoft.Extensions.DependencyInjection;
using SAF.Common;

namespace TestSequenceRunner
{
    public class TestAssemblyManifest : IServiceAssemblyManifest
    {
        public string FriendlyName { get; } = "TestSequenceRunner Test Manifest";

        public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
        {
            // do nothing
        }
    }
}