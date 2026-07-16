// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

public class PluginSystemControllerTests
{
    private readonly ILogger<PluginSystemController> _logger = Substitute.For<ILogger<PluginSystemController>>();
    private readonly IPluginServicesContainer _pluginServicesContainer = Substitute.For<IPluginServicesContainer>();

    private static IServiceProvider BuildProviderWith(params IServicePlugin[] servicePlugins)
    {
        var services = new ServiceCollection();
        foreach (IServicePlugin servicePlugin in servicePlugins)
        {
            services.AddSingleton(servicePlugin);
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ReloadAsync_StopsReinitializesAndStartsServicePlugins_InOrder()
    {
        // Arrange
        var servicePlugin = Substitute.For<IServicePlugin>();
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(servicePlugin)]);
        var controller = new PluginSystemController(_logger, _pluginServicesContainer);

        // Act
        await controller.ReloadAsync();

        // Assert
        Received.InOrder(() =>
        {
            servicePlugin.StopAsync(Arg.Any<CancellationToken>());
            _pluginServicesContainer.ReinitializeAsync(Arg.Any<CancellationToken>());
            servicePlugin.StartAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ReloadAsync_Reinitializes_WhenNoServicePluginsPresent()
    {
        // Arrange
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith()]);
        var controller = new PluginSystemController(_logger, _pluginServicesContainer);

        // Act
        await controller.ReloadAsync();

        // Assert
        await _pluginServicesContainer.Received(1).ReinitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReloadAsync_SwallowsPluginStopException_AndStillReinitializes()
    {
        // Arrange
        var faultyPlugin = Substitute.For<IServicePlugin>();
        faultyPlugin.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("boom")));
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(faultyPlugin)]);
        var controller = new PluginSystemController(_logger, _pluginServicesContainer);

        // Act
        var exception = await Record.ExceptionAsync(() => controller.ReloadAsync());

        // Assert
        Assert.Null(exception);
        await _pluginServicesContainer.Received(1).ReinitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReloadAsync_SwallowsPluginStartException()
    {
        // Arrange
        var faultyPlugin = Substitute.For<IServicePlugin>();
        faultyPlugin.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("boom")));
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(faultyPlugin)]);
        var controller = new PluginSystemController(_logger, _pluginServicesContainer);

        // Act
        var exception = await Record.ExceptionAsync(() => controller.ReloadAsync());

        // Assert
        Assert.Null(exception);
        await faultyPlugin.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }
}
