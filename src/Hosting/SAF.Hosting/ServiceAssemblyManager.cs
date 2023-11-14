using Microsoft.Extensions.Logging;
using SAF.Common;

namespace SAF.Hosting;

public interface IServiceAssemblyManager
{
    IEnumerable<IServiceAssemblyManifest> GetServiceAssemblyManifests();
}

internal class ServiceAssemblyManager(
    ILogger<ServiceAssemblyManager> logger,
    IServiceAssemblySearch? assemblySearch,
    IEnumerable<IServiceAssemblyManifest> serviceAssemblies)
    : IServiceAssemblyManager
{
    private readonly ILogger<ServiceAssemblyManager> _logger = logger;
    private readonly IServiceAssemblySearch? _assemblySearch = assemblySearch;
    private readonly IEnumerable<IServiceAssemblyManifest> _serviceAssemblies = serviceAssemblies;

    public IEnumerable<IServiceAssemblyManifest> GetServiceAssemblyManifests()
    {
        var loadedAssemblies = _assemblySearch?.LoadServiceAssemblyManifests();

        _logger.LogInformation("Registered {assembliesRegisteredCount} assemblies.", _serviceAssemblies.Count());
        return _serviceAssemblies.Concat(loadedAssemblies).ToList();
    }
}
