// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Routing.Tests;

using Microsoft.Extensions.DependencyInjection;
using SAF.Messaging.Contracts;
using Xunit;

public class AssemblyLoadingTests
{
    private RoutingConfiguration[] TestRoutings => new[]
    {
        new RoutingConfiguration
        {
            Messaging = new MessagingConfiguration { Key = "Redis" }
        }
    };

    [Fact]
    public void AddsRoutingInfrastructureAndPublicMessagingService()
    {
        var services = new ServiceCollection();

        services.AddKeyedSingleton<IMessagingInfrastructureFactory>("Redis",
            new DelegatingMessagingInfrastructureFactory("Redis", _ => new StubMessagingInfrastructure()));

        services.AddRoutingMessagingInfrastructure(config => config.Routings = TestRoutings);

        Assert.Contains(services, sd => sd.ServiceType == typeof(IRoutingMessagingInfrastructure));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IMessagingInfrastructureFactory) && sd.IsKeyedService && Equals(sd.ServiceKey, MessagingInfrastructureKeys.Routing));
    }

    [Fact]
    public void BuildsRoutingMessagingFromKeyedFactory()
    {
        var services = new ServiceCollection();
        var factory = new DelegatingMessagingInfrastructureFactory("Redis", _ => new StubMessagingInfrastructure());

        services.AddKeyedSingleton<IMessagingInfrastructureFactory>("Redis", factory);
        services.AddRoutingMessagingInfrastructure(config => config.Routings = TestRoutings);

        Assert.Contains(services, sd => sd.ServiceType == typeof(IMessagingInfrastructureFactory) && sd.IsKeyedService && Equals(sd.ServiceKey, "Redis"));
    }

    private sealed class StubMessagingInfrastructure : IMessagingInfrastructure
    {
        public void Publish(Message message)
        {
        }

        public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler => new object();

        public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler => new object();

        public object Subscribe(Action<Message> handler) => new object();

        public object Subscribe(string routeFilterPattern, Action<Message> handler) => new object();

        public void Unsubscribe(object subscription)
        {
        }
    }
}


