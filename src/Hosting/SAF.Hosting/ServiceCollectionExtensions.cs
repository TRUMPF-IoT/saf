// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SAF.Common;
using SAF.PluginSystem.Hosting.Contracts;

internal static class ServiceCollectionExtensions
{
    private const string HostIdStorageKey = "saf/hostid";

    /// <summary>
    /// Adds a <see cref="IServiceHostInfo"/> using options from the "ServiceHost" configuration section.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">An action to configure the <see cref="ServiceHostOptions"/>.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddServiceHostInfo(this IServiceCollection services, Action<ServiceHostOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddSingleton<IServiceHostInfo>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ServiceHostOptions>>().Value;
            return new ServiceHostInfo(options, () => GetOrInitializeHostId(sp.GetService<IStorageInfrastructure>()));
        });

        // Bridge: forward the configured service into every plugin container.
        // Runs before each plugin manifest's ConfigureServices, so plugins always receive
        // the IServiceHostInfo that includes all code-based Configure<ServiceHostOptions> calls.
        services.AddSingleton<IHostServiceForwarder, HostServiceForwarder<IServiceHostInfo>>();

        return services;
    }

    private static string GetOrInitializeHostId(IStorageInfrastructure? storage)
    {
        var id = storage?.GetString(HostIdStorageKey);
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
            storage?.Set(HostIdStorageKey, id);
        }

        return id;
    }
}
