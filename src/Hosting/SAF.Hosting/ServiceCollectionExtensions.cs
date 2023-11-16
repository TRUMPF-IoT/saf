// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using SAF.Common;
using SAF.Hosting.Abstractions;

namespace SAF.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceHostBuilder AddHost(this IServiceCollection services)
        => services.AddHostCore()
            .AddServiceAssemblySearch();

    public static IServiceHostBuilder AddHost(this IServiceCollection services, Action<ServiceAssemblySearchOptions> configure)
        => services.AddHost(configure, null);

    public static IServiceHostBuilder AddHost(this IServiceCollection services, Action<ServiceAssemblySearchOptions> configure, Action<ServiceHostInfoOptions>? configureHostInfo)
    {
        var builder = services.AddHostCore()
            .AddServiceAssemblySearch(configure);

        if(configureHostInfo != null)
            builder.ConfigureServiceHostInfo(configureHostInfo);

        return builder;
    }

    public static IServiceHostBuilder AddHostCore(this IServiceCollection services)
    {
        services.AddSingleton<IServiceAssemblyManager, ServiceAssemblyManager>();
        services.AddSingleton<IServiceMessageDispatcher, ServiceMessageDispatcher>();

        services.AddSingleton<ServiceHost>();
        services.AddHostedService(sp => sp.GetRequiredService<ServiceHost>());

        var builder = new ServiceHostBuilder(services);
        builder.AddServiceHostInfo();

        return builder;
    }
}