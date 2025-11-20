// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Common;
using Contracts;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Builds a default SAF service host searching for assemblies in the default locations and using default service host information.
    /// </summary>
    /// <param name="services">The service collection to add the services.</param>
    /// <returns>The <see cref="IServiceHostBuilder"/>.</returns>
    public static IServiceHostBuilder AddHost(this IServiceCollection services)
        => services.AddHost(_ => {});

    /// <summary>
    /// Builds a default SAF service host searching for assemblies in configurable locations and using default service host information.
    /// </summary>
    /// <param name="services">The service collection to add the services.</param>
    /// <param name="configure">The action to configure the default assembly search location.</param>
    /// <returns>The <see cref="IServiceHostBuilder"/>.</returns>
    public static IServiceHostBuilder AddHost(this IServiceCollection services, Action<ServiceAssemblySearchOptions> configure)
        => services.AddHost(configure, _ => {});

    /// <summary>
    /// Builds a default SAF service host searching for assemblies in configurable locations and with configurable information about the service host.
    /// </summary>
    /// <param name="services">The service collection to add the services.</param>
    /// <param name="configure">The action to configure the default assembly search location.</param>
    /// <param name="configureHostInfo">The action to configure the service host information.</param>
    /// <returns>The <see cref="IServiceHostBuilder"/>.</returns>
    public static IServiceHostBuilder AddHost(this IServiceCollection services, Action<ServiceAssemblySearchOptions> configure, Action<ServiceHostInfoOptions> configureHostInfo)
    {
        var builder = services.AddHostCore()
            .AddServiceAssemblySearch(configure);

        builder.ConfigureServiceHostInfo(configureHostInfo);

        return builder;
    }

    /// <summary>
    /// Adds SAF hosting core services to the DI container.
    /// </summary>
    /// <param name="services">The service collection to add the services.</param>
    /// <returns>The <see cref="IServiceHostBuilder"/>.</returns>
    public static IServiceHostBuilder AddHostCore(this IServiceCollection services)
    {
        services.AddSingleton<IServiceAssemblyManager, ServiceAssemblyManager>();
        services.AddSingleton<IServiceMessageDispatcher, ServiceMessageDispatcher>();

        services.AddSingleton<IServiceMessageHandlerTypes, ServiceMessageHandlerTypes>(_ => new ServiceMessageHandlerTypes(services));
        services.AddSingleton<ServiceHost>();
        services.AddHostedService(sp => sp.GetRequiredService<ServiceHost>());

        var builder = new ServiceHostBuilder(services);
        builder.AddServiceHostInfo();

        return builder;
    }
}