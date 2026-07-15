// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Extensions.Tests;

using Microsoft.Extensions.DependencyInjection;
using SAF.Messaging.Contracts;
using Xunit;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMessageHandlerResolver_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(() => services!.AddMessageHandlerResolver());
    }

    [Fact]
    public void AddMessageHandlerResolver_WhenHandlerRegisteredOnlyAsInterface_ReturnsNullForConcreteHandlerType()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageHandler, TestHandler>();
        services.AddMessageHandlerResolver();

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IMessageHandlerResolver>();

        var resolvedHandler = resolver.Resolve(typeof(TestHandler));

        Assert.Null(resolvedHandler);
    }

    [Fact]
    public void AddMessageHandlerResolver_WhenHandlerRegistered_ResolvesMatchingSingletonHandlerType_ByType()
    {
        var services = new ServiceCollection();
        services.AddSingletonMessageHandler<TestHandler>();
        services.AddMessageHandlerResolver();

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IMessageHandlerResolver>();

        var resolvedHandler = resolver.Resolve(typeof(TestHandler));

        Assert.IsType<TestHandler>(resolvedHandler);
    }

    [Fact]
    public void AddMessageHandlerResolver_WhenHandlerRegistered_ResolvesMatchingTransientHandlerType_ByType()
    {
        var services = new ServiceCollection();
        services.AddTransientMessageHandler<TestHandler>();
        services.AddMessageHandlerResolver();

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IMessageHandlerResolver>();

        var resolvedHandler = resolver.Resolve(typeof(TestHandler));

        Assert.IsType<TestHandler>(resolvedHandler);
    }

    [Fact]
    public void AddMessageHandlerResolver_WhenHandlerTypeNotRegistered_ReturnsNull()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageHandler, TestHandler>();
        services.AddMessageHandlerResolver();

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IMessageHandlerResolver>();

        Assert.Null(resolver.Resolve(typeof(UnknownHandler)));
    }

    private sealed class TestHandler : IMessageHandler
    {
        public bool CanHandle(Message message) => true;

        public void Handle(Message message)
        {
        }
    }

    private sealed class UnknownHandler : IMessageHandler
    {
        public bool CanHandle(Message message) => false;

        public void Handle(Message message)
        {
        }
    }
}
