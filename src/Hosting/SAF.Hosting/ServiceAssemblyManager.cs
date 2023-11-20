using Microsoft.Extensions.Logging;
using SAF.Hosting.Abstractions;

namespace SAF.Hosting;

internal class ServiceAssemblyManager(ILogger<ServiceAssemblyManager> logger,
        IServiceAssemblySearch? assemblySearch,
        IEnumerable<IServiceAssemblyManifest> serviceAssemblies)
    : IServiceAssemblyManager
{
    public IEnumerable<IServiceAssemblyManifest> GetServiceAssemblyManifests()
    {
        var loadedAssemblies = assemblySearch?.LoadServiceAssemblyManifests();

        logger.LogInformation("Registered {assembliesRegisteredCount} assemblies.", serviceAssemblies.Count());
        return serviceAssemblies
            .Concat(loadedAssemblies ?? Array.Empty<IServiceAssemblyManifest>())
            .ToList();
    }
}