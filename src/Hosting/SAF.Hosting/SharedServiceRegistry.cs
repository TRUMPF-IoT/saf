using Microsoft.Extensions.DependencyInjection;
using SAF.Hosting.Abstractions;

namespace SAF.Hosting;

internal class SharedServiceRegistry : ISharedServiceRegistry
{
    internal IServiceCollection SharedServices { get; } = new ServiceCollection();

    public IEnumerable<ServiceDescriptor> Services => SharedServices;
}