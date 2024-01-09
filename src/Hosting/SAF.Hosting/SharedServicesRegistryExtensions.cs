// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Abstractions;

internal static class SharedServicesRegistryExtensions
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
                    throw new InvalidOperationException($"Unknown lifetime {serviceDescriptor.Lifetime} of shared service {serviceDescriptor.ServiceType.Name}");
            }
        }
    }
}