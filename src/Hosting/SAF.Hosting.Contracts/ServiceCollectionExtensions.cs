// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Contracts;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Some extension methods to simplify service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a hosted service to the container.
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    /// <param name="serviceCollection">The service collection to add the service.</param>
    /// <returns>The serviceCollection for chaining.</returns>
    [Obsolete("AddHosted will be removed in a future release. Use AddHostedAsync instead.")]
    public static IServiceCollection AddHosted<TService>(this IServiceCollection serviceCollection)
        where TService : class, IHostedService
    {
        serviceCollection.AddSingleton<IHostedService, TService>();

        return serviceCollection;
    }

    /// <summary>
    /// Adds a hosted service to the container that supports async start/stop.
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    /// <param name="serviceCollection">The service collection to add the service.</param>
    /// <returns>The serviceCollection for chaining.</returns>
    public static IServiceCollection AddHostedAsync<TService>(this IServiceCollection serviceCollection)
        where TService : class, IHostedServiceAsync
    {
        serviceCollection.AddSingleton<IHostedServiceAsync, TService>();

        return serviceCollection;
    }
}