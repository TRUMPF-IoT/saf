// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using Microsoft.Extensions.DependencyInjection;

namespace SAF.Common
{
    /// <summary>
    ///     Some extension methods to simplify service registration.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Adds a hosted service to the container.
        /// </summary>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <param name="serviceCollection">The service collection to add the service.</param>
        /// <returns>The serviceCollection for chaining.</returns>
        public static IServiceCollection AddHosted<TService>(this IServiceCollection serviceCollection)
            where TService : IHostedService
            => serviceCollection.AddSingleton(typeof(IHostedService), typeof(TService));
    }
}