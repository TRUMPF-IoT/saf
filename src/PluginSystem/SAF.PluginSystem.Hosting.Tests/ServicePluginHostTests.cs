// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.Logging;
using NSubstitute;

public class ServicePluginHostTests
{
    private readonly ILogger<ServicePluginHost> _logger = Substitute.For<ILogger<ServicePluginHost>>();
    private readonly IServiceProvider _pluginServiceProvider = Substitute.For<IServiceProvider>();
    private readonly IPluginServicesContainer _pluginServicesContainer = Substitute.For<IPluginServicesContainer>();

    public ServicePluginHostTests()
    {
        _pluginServicesContainer.GetPluginServices().Returns([_pluginServiceProvider]);
    }

    [Fact]
    public async Task StartAsync_ShouldStartAllServicePlugins()
    {
        // Arrange
        var servicePlugin1 = Substitute.For<IServicePlugin>();
        var servicePlugin2 = Substitute.For<IServicePlugin>();
        _pluginServiceProvider.GetService(Arg.Is(typeof(IEnumerable<IServicePlugin>)))
            .Returns(new List<IServicePlugin>() { servicePlugin1, servicePlugin2 });

        var service = new ServicePluginHost(_logger, _pluginServicesContainer);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        await servicePlugin1.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await servicePlugin2.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_ShouldStopAllServicePlugins()
    {
        // Arrange
        var servicePlugin1 = Substitute.For<IServicePlugin>();
        var servicePlugin2 = Substitute.For<IServicePlugin>();
        _pluginServiceProvider.GetService(Arg.Is(typeof(IEnumerable<IServicePlugin>)))
            .Returns(new List<IServicePlugin>() { servicePlugin1, servicePlugin2 });

        var service = new ServicePluginHost(_logger, _pluginServicesContainer);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        await servicePlugin1.Received(1).StopAsync(Arg.Any<CancellationToken>());
        await servicePlugin2.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_ShouldLogError_WhenPluginStartFails()
    {
        // Arrange
        var servicePlugin = Substitute.For<IServicePlugin>();
        servicePlugin.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("Start failed")));
        _pluginServiceProvider.GetService(Arg.Is(typeof(IEnumerable<IServicePlugin>)))
            .Returns(new List<IServicePlugin>() { servicePlugin });

        var service = new ServicePluginHost(_logger, _pluginServicesContainer);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _logger.Received(1).LogError(Arg.Any<Exception?>(), "Failed to start service plug-in.");
    }

    [Fact]
    public async Task StopAsync_ShouldLogError_WhenPluginStopFails()
    {
        // Arrange
        var servicePlugin = Substitute.For<IServicePlugin>();
        servicePlugin.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("Stop failed")));
        _pluginServiceProvider.GetService(Arg.Is(typeof(IEnumerable<IServicePlugin>)))
            .Returns(new List<IServicePlugin>() { servicePlugin });

        var service = new ServicePluginHost(_logger, _pluginServicesContainer);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        _logger.Received(1).LogError(Arg.Any<Exception?>(), "Failed to stop service plug-in.");
    }

    [Fact]
    public async Task StartingAsync_ShouldCallStartingAsyncOnAllLifecyclePlugins()
    {
        // Arrange
        var lifecyclePlugin1 = Substitute.For<ILifecycleServicePlugin>();
        var lifecyclePlugin2 = Substitute.For<ILifecycleServicePlugin>();
        _pluginServiceProvider.GetService(Arg.Is(typeof(IEnumerable<IServicePlugin>)))
            .Returns(new List<IServicePlugin>() { lifecyclePlugin1, lifecyclePlugin2 });

        var service = new ServicePluginHost(_logger, _pluginServicesContainer);

        // Act
        await service.StartingAsync(CancellationToken.None);

        // Assert
        await lifecyclePlugin1.Received(1).StartingAsync(Arg.Any<CancellationToken>());
        await lifecyclePlugin2.Received(1).StartingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartedAsync_ShouldCallStartedAsyncOnAllLifecyclePlugins()
    {
        // Arrange
        var lifecyclePlugin1 = Substitute.For<ILifecycleServicePlugin>();
        var lifecyclePlugin2 = Substitute.For<ILifecycleServicePlugin>();
        _pluginServiceProvider.GetService(Arg.Is(typeof(IEnumerable<IServicePlugin>)))
            .Returns(new List<IServicePlugin>() { lifecyclePlugin1, lifecyclePlugin2 });

        var service = new ServicePluginHost(_logger, _pluginServicesContainer);

        // Act
        await service.StartedAsync(CancellationToken.None);

        // Assert
        await lifecyclePlugin1.Received(1).StartedAsync(Arg.Any<CancellationToken>());
        await lifecyclePlugin2.Received(1).StartedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoppingAsync_ShouldCallStoppingAsyncOnAllLifecyclePlugins()
    {
        // Arrange
        var lifecyclePlugin1 = Substitute.For<ILifecycleServicePlugin>();
        var lifecyclePlugin2 = Substitute.For<ILifecycleServicePlugin>();
        _pluginServiceProvider.GetService(Arg.Is(typeof(IEnumerable<IServicePlugin>)))
            .Returns(new List<IServicePlugin>() { lifecyclePlugin1, lifecyclePlugin2 });

        var service = new ServicePluginHost(_logger, _pluginServicesContainer);

        // Act
        await service.StoppingAsync(CancellationToken.None);

        // Assert
        await lifecyclePlugin1.Received(1).StoppingAsync(Arg.Any<CancellationToken>());
        await lifecyclePlugin2.Received(1).StoppingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoppedAsync_ShouldCallStoppedAsyncOnAllLifecyclePlugins()
    {
        // Arrange
        var lifecyclePlugin1 = Substitute.For<ILifecycleServicePlugin>();
        var lifecyclePlugin2 = Substitute.For<ILifecycleServicePlugin>();
        _pluginServiceProvider.GetService(Arg.Is(typeof(IEnumerable<IServicePlugin>)))
            .Returns(new List<IServicePlugin>() { lifecyclePlugin1, lifecyclePlugin2 });

        var service = new ServicePluginHost(_logger, _pluginServicesContainer);

        // Act
        await service.StoppedAsync(CancellationToken.None);

        // Assert
        await lifecyclePlugin1.Received(1).StoppedAsync(Arg.Any<CancellationToken>());
        await lifecyclePlugin2.Received(1).StoppedAsync(Arg.Any<CancellationToken>());
    }
}