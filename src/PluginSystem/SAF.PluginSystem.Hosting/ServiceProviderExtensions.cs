// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.IO.Abstractions;

internal static class ServiceProviderExtensions
{
    public static IServiceProvider RedirectCommonServices(this IServiceProvider sp, IServiceCollection services)
    {
        services.AddSingleton(_ => sp.GetRequiredService<ILoggerFactory>());
        services.AddTransient(_ => sp.GetRequiredService<ILogger>());
        services.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        services.AddSingleton(_ => sp.GetRequiredService<IPluginServiceProvider>());
        services.AddSingleton(_ => sp.GetRequiredService<IPluginSystemHostEnvironment>());

        services.AddSingleton(_ => sp.GetRequiredService<IFileSystem>());

        return sp;
    }

    public static IPublicPluginServiceFactory? FindPublicPluginServiceFactory(this IServiceProvider sp, ServiceDescriptor serviceDescriptor)
    {
        var factoryType = typeof(PublicPluginServiceFactory<>).MakeGenericType(serviceDescriptor.ServiceType);
        var enumerableFactoryType = typeof(IEnumerable<>).MakeGenericType(factoryType);

        IEnumerable? factories;
        if (serviceDescriptor.IsKeyedService && sp is IKeyedServiceProvider keyedServiceProvider)
        {
            factories = keyedServiceProvider.GetKeyedService(enumerableFactoryType, serviceDescriptor.ServiceKey) as IEnumerable;
        }
        else
        {
            factories = sp.GetService(enumerableFactoryType) as IEnumerable;
        }

        if (factories is null)
        {
            return null;
        }

        foreach (var factory in factories)
        {
            if (factory is IPublicPluginServiceFactory publicServiceFactory &&
                publicServiceFactory.ServiceDescriptor == serviceDescriptor)
            {
                return publicServiceFactory;
            }
        }

        return null;
    }
}