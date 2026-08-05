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
    private static ServiceCollection BuildHostServices(
        ILoggerFactory? loggerFactory = null,
        IPluginServiceProvider? pluginServiceProvider = null,
        IPluginSystemHostEnvironment? hostEnvironment = null,
        IFileSystem? fileSystem = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory ?? NullLoggerFactory.Instance);
        services.AddTransient(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(pluginServiceProvider ?? Substitute.For<IPluginServiceProvider>());
        services.AddSingleton(hostEnvironment ?? Substitute.For<IPluginSystemHostEnvironment>());
        services.AddSingleton(fileSystem ?? Substitute.For<IFileSystem>());
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

    [Fact]
    public void RedirectCommonServices_WhenPluginProviderDisposed_DoesNotDisposeHostLoggerFactory()
    {
        var hostLoggerFactory = Substitute.For<ILoggerFactory>();
        var hostServices = BuildHostServices();
        hostServices.AddSingleton(hostLoggerFactory);
        var hostProvider = hostServices.BuildServiceProvider();
        var pluginServices = new ServiceCollection();
        hostProvider.RedirectCommonServices(pluginServices);

        var pluginProvider = pluginServices.BuildServiceProvider();
        _ = pluginProvider.GetRequiredService<ILoggerFactory>();

        pluginProvider.Dispose();

        hostLoggerFactory.DidNotReceive().Dispose();
    }

    [Fact]
    public void RedirectCommonServices_WhenPluginProviderDisposed_DoesNotDisposeHostPluginServiceProvider()
    {
        var hostPluginServiceProvider = Substitute.For<IPluginServiceProvider, IDisposable>();
        var hostServices = BuildHostServices(pluginServiceProvider: hostPluginServiceProvider);
        var hostProvider = hostServices.BuildServiceProvider();
        var pluginServices = new ServiceCollection();
        hostProvider.RedirectCommonServices(pluginServices);

        var pluginProvider = pluginServices.BuildServiceProvider();
        _ = pluginProvider.GetRequiredService<IPluginServiceProvider>();

        pluginProvider.Dispose();

        ((IDisposable)hostPluginServiceProvider).DidNotReceive().Dispose();
    }

    [Fact]
    public void RedirectCommonServices_WhenPluginProviderDisposed_DoesNotDisposeHostEnvironment()
    {
        var hostEnvironment = Substitute.For<IPluginSystemHostEnvironment, IDisposable>();
        var hostServices = BuildHostServices(hostEnvironment: hostEnvironment);
        var hostProvider = hostServices.BuildServiceProvider();
        var pluginServices = new ServiceCollection();
        hostProvider.RedirectCommonServices(pluginServices);

        var pluginProvider = pluginServices.BuildServiceProvider();
        _ = pluginProvider.GetRequiredService<IPluginSystemHostEnvironment>();

        pluginProvider.Dispose();

        ((IDisposable)hostEnvironment).DidNotReceive().Dispose();
    }

    [Fact]
    public void RedirectCommonServices_WhenPluginProviderDisposed_DoesNotDisposeHostFileSystem()
    {
        var hostFileSystem = Substitute.For<IFileSystem, IDisposable>();
        var hostServices = BuildHostServices(fileSystem: hostFileSystem);
        var hostProvider = hostServices.BuildServiceProvider();
        var pluginServices = new ServiceCollection();
        hostProvider.RedirectCommonServices(pluginServices);

        var pluginProvider = pluginServices.BuildServiceProvider();
        _ = pluginProvider.GetRequiredService<IFileSystem>();

        pluginProvider.Dispose();

        ((IDisposable)hostFileSystem).DidNotReceive().Dispose();
    }

    [Fact]
    public void RedirectCommonServices_ResolvesLoggerFactoryLazily()
    {
        var hostLoggerFactory = Substitute.For<ILoggerFactory>();
        var hostServices = BuildHostServices();
        var loggerFactoryResolutions = 0;
        hostServices.AddSingleton<ILoggerFactory>(_ =>
        {
            loggerFactoryResolutions++;
            return hostLoggerFactory;
        });
        var hostProvider = hostServices.BuildServiceProvider();

        var pluginServices = new ServiceCollection();
        hostProvider.RedirectCommonServices(pluginServices);

        Assert.Equal(0, loggerFactoryResolutions);

        var pluginProvider = pluginServices.BuildServiceProvider();
        Assert.Equal(0, loggerFactoryResolutions);

        _ = pluginProvider.GetRequiredService<ILoggerFactory>();
        Assert.Equal(1, loggerFactoryResolutions);
    }
}
