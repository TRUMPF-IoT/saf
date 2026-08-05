// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
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
        services.AddSingleton<ILoggerFactory>(_ => new NonOwningLoggerFactory(sp.GetRequiredService<ILoggerFactory>()));
        services.AddTransient(_ => sp.GetRequiredService<ILogger>());
        services.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        services.AddForwardedNonOwningSingleton<IPluginServiceProvider>(sp);
        services.AddForwardedNonOwningSingleton<IPluginSystemHostEnvironment>(sp);
        services.AddForwardedNonOwningSingleton<IFileSystem>(sp);

        foreach (var forwarder in sp.GetService<IEnumerable<IHostServiceForwarder>>() ?? [])
        {
            forwarder.Forward(services);
        }

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

    private static void AddForwardedNonOwningSingleton<TService>(this IServiceCollection services, IServiceProvider hostProvider)
        where TService : class
        => services.AddSingleton(_ =>
        {
            var hostService = hostProvider.GetRequiredService<TService>();
            return (TService)NonOwningServiceProxy.WrapIfRequired(hostService, typeof(TService));
        });
}