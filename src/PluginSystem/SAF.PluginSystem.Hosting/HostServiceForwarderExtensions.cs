// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using AssemblyLoading;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers host services that are forwarded into every plugin container.
/// </summary>
public static class HostServiceForwarderExtensions
{
    /// <summary>
    /// Forwards the host-registered <typeparamref name="T"/> into every plugin container and shares the
    /// assembly declaring <typeparamref name="T"/>, so the plugin resolves the same type identity the host
    /// registered. Registering both together is what keeps a forwarded service from silently binding to a
    /// plugin-private copy of its contract assembly.
    /// </summary>
    /// <typeparam name="T">The host service type to forward.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddHostServiceForwarder<T>(this IServiceCollection services)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostServiceForwarder, HostServiceForwarder<T>>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISharedAssemblySource, SharedAssemblySource<T>>());

        return services;
    }
}
