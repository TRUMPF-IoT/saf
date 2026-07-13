// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.IO.Abstractions;
using Xunit;

public class ServiceProviderExtensionsTests
{
    // Minimal host service collection that satisfies RedirectCommonServices' required resolutions.
    private static ServiceCollection BuildHostServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddTransient(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(Substitute.For<IPluginServiceProvider>());
        services.AddSingleton(Substitute.For<IPluginSystemHostEnvironment>());
        services.AddSingleton(Substitute.For<IFileSystem>());
        return services;
    }

    [Fact]
    public void RedirectCommonServices_WhenNoForwardersRegistered_DoesNotThrow()
    {
        var hostProvider = BuildHostServices().BuildServiceProvider();
        var pluginServices = new ServiceCollection();

        var exception = Record.Exception(() => hostProvider.RedirectCommonServices(pluginServices));

        Assert.Null(exception);
    }

    [Fact]
    public void RedirectCommonServices_CallsSingleRegisteredForwarder()
    {
        var forwarder = Substitute.For<IHostServiceForwarder>();
        var hostServices = BuildHostServices();
        hostServices.AddSingleton<IHostServiceForwarder>(forwarder);
        var hostProvider = hostServices.BuildServiceProvider();
        var pluginServices = new ServiceCollection();

        hostProvider.RedirectCommonServices(pluginServices);

        forwarder.Received(1).Forward(pluginServices);
    }

    [Fact]
    public void RedirectCommonServices_CallsAllRegisteredForwarders()
    {
        var forwarder1 = Substitute.For<IHostServiceForwarder>();
        var forwarder2 = Substitute.For<IHostServiceForwarder>();
        var hostServices = BuildHostServices();
        hostServices.AddSingleton<IHostServiceForwarder>(forwarder1);
        hostServices.AddSingleton<IHostServiceForwarder>(forwarder2);
        var hostProvider = hostServices.BuildServiceProvider();
        var pluginServices = new ServiceCollection();

        hostProvider.RedirectCommonServices(pluginServices);

        forwarder1.Received(1).Forward(pluginServices);
        forwarder2.Received(1).Forward(pluginServices);
    }

    [Fact]
    public void RedirectCommonServices_ForwarderReceivesPluginServiceCollection()
    {
        IServiceCollection? capturedCollection = null;
        var forwarder = Substitute.For<IHostServiceForwarder>();
        forwarder.When(f => f.Forward(Arg.Any<IServiceCollection>()))
                 .Do(ci => capturedCollection = ci.Arg<IServiceCollection>());

        var hostServices = BuildHostServices();
        hostServices.AddSingleton<IHostServiceForwarder>(forwarder);
        var hostProvider = hostServices.BuildServiceProvider();
        var pluginServices = new ServiceCollection();

        hostProvider.RedirectCommonServices(pluginServices);

        Assert.Same(pluginServices, capturedCollection);
    }
}
