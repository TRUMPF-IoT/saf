// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAF.Common;

public static class ServiceCollectionExtensions
{
    private const string ServiceHostSectionName = "ServiceHost";
    private const string HostIdStorageKey = "saf/hostid";

    /// <summary>
    /// Adds a legacy-compatible <see cref="IServiceHostInfo"/> with default options.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddServiceHostInfo(this IServiceCollection services)
        => services.AddServiceHostInfo(static _ => { });

    /// <summary>
    /// Adds a legacy-compatible <see cref="IServiceHostInfo"/> using options from the "ServiceHost" configuration section.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The root configuration used to bind service host options.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddServiceHostInfo(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddServiceHostInfo(configuration.GetSection(ServiceHostSectionName).Bind);
    }

    /// <summary>
    /// Adds a legacy-compatible <see cref="IServiceHostInfo"/> using the provided options callback.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Callback to configure host info options.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddServiceHostInfo(this IServiceCollection services, Action<ServiceHostOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSingleton<IServiceHostInfo>(sp =>
        {
            var options = new ServiceHostOptions();
            configure(options);

            return new ServiceHostInfo(options, () => GetOrInitializeHostId(sp.GetService<IStorageInfrastructure>()));
        });

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
