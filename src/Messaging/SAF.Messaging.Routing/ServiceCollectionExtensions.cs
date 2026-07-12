// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Common;
using SAF.Messaging.Contracts;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SAF.Messaging.Routing.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace SAF.Messaging.Routing;

/// <summary>
///     Some extension methods to simplify service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a IMessagingInfrastructure to the container used to provide message routing.
    /// </summary>
    /// <param name="serviceCollection">The service collection to add the IMessagingInfrastructure.</param>
    /// <param name="configure">Action used to update configuration for message routes.</param>
    /// <returns>The serviceCollection for chaining.</returns>
    public static IServiceCollection AddRoutingMessagingInfrastructure(this IServiceCollection serviceCollection, Action<Configuration> configure)
    {
        var config = new Configuration();
        configure(config);

        return serviceCollection.AddRoutingMessagingInfrastructure(config)
            .AddSingleton<IMessagingInfrastructure>(sp => sp.GetRequiredService<IRoutingMessagingInfrastructure>());
    }

    private static IServiceCollection AddRoutingMessagingInfrastructure(this IServiceCollection serviceCollection, Configuration config)
        => serviceCollection.AddTransient<IRoutingMessagingInfrastructure>(sp =>
            new Messaging(sp.GetService<ILogger<Messaging>>(), BuildMessageRouting(sp, config)));

    private static MessageRouting[] BuildMessageRouting(IServiceProvider serviceProvider, Configuration config)
    {
        return config.Routings
            .Select(r =>
            {
                var factory = serviceProvider.GetRequiredKeyedService<IMessagingInfrastructureFactory>(r.Messaging.Key);
                var routing = new MessageRouting(factory.Create(r.Messaging))
                {
                    PublishPatterns = r.PublishPatterns,
                    SubscriptionPatterns = r.SubscriptionPatterns
                };
                return routing;
            }).ToArray();
    }
}


