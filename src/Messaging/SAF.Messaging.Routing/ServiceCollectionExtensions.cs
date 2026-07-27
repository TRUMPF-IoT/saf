// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            .AddKeyedSingleton<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Routing,
                (sp, _) => new DelegatingMessagingInfrastructureFactory(
                    MessagingInfrastructureKeys.Routing,
                    _ => CreateRoutingMessagingInfrastructure(sp, config)));
    }

    private static IServiceCollection AddRoutingMessagingInfrastructure(this IServiceCollection serviceCollection, Configuration config)
        => serviceCollection.AddTransient<Messaging>(sp =>
            CreateRoutingMessagingInfrastructure(sp, config));

    private static Messaging CreateRoutingMessagingInfrastructure(IServiceProvider serviceProvider, Configuration config)
        => new Messaging(serviceProvider.GetService<ILogger<Messaging>>(), BuildMessageRouting(serviceProvider, config));

    private static MessageRouting[] BuildMessageRouting(IServiceProvider serviceProvider, Configuration config)
    {
        if (config.Routings.Any(r => string.Equals(r.Messaging.Key, MessagingInfrastructureKeys.Routing, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Route configuration must not use messaging key '{MessagingInfrastructureKeys.Routing}' because it causes recursive routing infrastructure creation.");
        }

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


