// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting.Extensions;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Contracts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServicePlugin<TService>(this IServiceCollection services)
        where TService : class, IServicePlugin
    {
        services.AddSingleton<IServicePlugin, TService>();
        return services;
    }

    public static IServiceCollection AddKeyedOptions<TOptions>(this IServiceCollection services, string key, Action<TOptions> configureOptions)
        where TOptions : class
    {
        services.Configure(key, configureOptions);
        services.AddKeyedTransient(key, (sp, _) => sp.GetRequiredService<IOptionsMonitor<TOptions>>().Get(key));

        return services;
    }
}