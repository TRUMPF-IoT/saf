using Microsoft.Extensions.DependencyInjection;

namespace SAF.Hosting.Abstractions;

/// <summary>
/// An interface for accessing SAF shared services like infrastructure services.
/// Such services will be registered using IServiceHostBuilder.AddSharedSingleton.
/// </summary>
public interface ISharedServiceRegistry
{
    /// <summary>
    /// Gets the shared services registered with this instance.
    /// </summary>
    IEnumerable<ServiceDescriptor> Services { get; }
}