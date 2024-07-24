// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Contracts;

internal static class SharedServicesRegistryExtensions
{
    public static void RedirectServices(this ISharedServiceRegistry registry, IServiceProvider source, IServiceCollection target)
    {
        foreach (var serviceDescriptor in registry.Services)
        {
            switch (serviceDescriptor.Lifetime)
            {
                case ServiceLifetime.Singleton:
                    target.AddSingleton(serviceDescriptor.ServiceType, _ => source.GetRequiredService(serviceDescriptor.ServiceType));
                    break;
                case ServiceLifetime.Scoped:
                    throw new InvalidOperationException($"Scoped service is not supported. Service: {serviceDescriptor.ServiceType.Name}");
                case ServiceLifetime.Transient:
                    target.AddTransient(serviceDescriptor.ServiceType, _ => source.GetRequiredService(serviceDescriptor.ServiceType));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown lifetime {serviceDescriptor.Lifetime} of shared service {serviceDescriptor.ServiceType.Name}");
            }
        }
    }
}