// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the C-DEngine configuration and a <see cref="CdeNodeLease"/> on the node of the process.
    /// </summary>
    /// <remarks>
    /// C-DEngine starts when the lease is first resolved, not here. All containers share one node, which
    /// runs with the configuration of the container that started it.
    /// </remarks>
    /// <param name="services">The plug-in service collection.</param>
    /// <param name="configure">Fills the C-DEngine configuration, typically by binding the <c>Cde</c> section.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddCde(this IServiceCollection services, Action<CdeConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        var config = new CdeConfiguration();
        configure?.Invoke(config);

        // Registered as an instance, so the container does not own the node: it outlives every container.
        services.TryAddSingleton(CdeNode.Shared);

        return services
            .AddSingleton(config)
            .AddSingleton(sp => sp.GetRequiredService<ICdeNode>()
                .Acquire(config, sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance));
    }
}