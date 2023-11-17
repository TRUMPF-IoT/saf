using Microsoft.Extensions.DependencyInjection;
using SAF.Hosting.Abstractions;

namespace SAF.Hosting;

internal class SharedServiceRegistry : ISharedServiceRegistry
{
    internal IServiceCollection SharedServices { get; } = new ServiceCollection();

    public IEnumerable<ServiceDescriptor> Services => SharedServices;
}

internal static class CommonServicesRegistryExtensions
{
    public static void RedirectServicesTo(this ISharedServiceRegistry registry, IServiceProvider source, IServiceCollection target)
    {
        foreach (var serviceDescriptor in registry.Services)
        {
            switch (serviceDescriptor.Lifetime)
            {
                case ServiceLifetime.Singleton:
                    target.AddSingleton(serviceDescriptor.ServiceType, _ => source.GetRequiredService(serviceDescriptor.ServiceType));
                    break;
                case ServiceLifetime.Scoped:
                    target.AddScoped(serviceDescriptor.ServiceType, _ => source.GetRequiredService(serviceDescriptor.ServiceType));
                    break;
                case ServiceLifetime.Transient:
                    target.AddTransient(serviceDescriptor.ServiceType, _ => source.GetRequiredService(serviceDescriptor.ServiceType));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown lifetime {serviceDescriptor.Lifetime} of common service {serviceDescriptor.ServiceType.Name}");
            }
        }
    }
}