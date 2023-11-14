// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using Microsoft.Extensions.DependencyInjection;

namespace SAF.Common;

/// <summary>
///     Some extension methods to simplify service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a hosted service to the container.
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    /// <param name="serviceCollection">The service collection to add the service.</param>
    /// <returns>The serviceCollection for chaining.</returns>
    public static IServiceCollection AddHosted<TService>(this IServiceCollection serviceCollection)
        where TService : class, IHostedService
    {
        serviceCollection.AddHostedServiceBase<TService>();
        serviceCollection.AddSingleton<IHostedService>(sp => sp.GetRequiredService<TService>());

        return serviceCollection;
    }

    /// <summary>
    /// Adds a hosted service to the container that supports async Start/Stop.
    /// </summary>
    /// <typeparam name="TService">The type of the service.</typeparam>
    /// <param name="serviceCollection">The service collection to add the service.</param>
    /// <returns>The serviceCollection for chaining.</returns>
    public static IServiceCollection AddHostedAsync<TService>(this IServiceCollection serviceCollection)
        where TService : class, IHostedServiceAsync
    {
        serviceCollection.AddHostedServiceBase<TService>();
        serviceCollection.AddSingleton<IHostedServiceAsync>(sp => sp.GetRequiredService<TService>());

        return serviceCollection;
    }

    private static IServiceCollection AddHostedServiceBase<TService>(this IServiceCollection serviceCollection)
        where TService : class, IHostedServiceBase
    {
        serviceCollection.AddSingleton<TService>();
        serviceCollection.AddSingleton<IHostedServiceBase>(sp => sp.GetRequiredService<TService>());

        return serviceCollection;
    }
}