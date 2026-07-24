// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

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
