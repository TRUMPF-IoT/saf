// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

public class ServicePluginLifecycleRunnerTests
{
    private readonly ILogger<ServicePluginLifecycleRunner> _logger = Substitute.For<ILogger<ServicePluginLifecycleRunner>>();
    private readonly IServiceProvider _pluginServiceProvider = Substitute.For<IServiceProvider>();
    private readonly IPluginServicesContainer _pluginServicesContainer = Substitute.For<IPluginServicesContainer>();

    public ServicePluginLifecycleRunnerTests()
    {
        _pluginServicesContainer.GetPluginServices().Returns([_pluginServiceProvider]);
    }

    private static ServiceProvider BuildProviderWith(params IServicePlugin[] servicePlugins)
    {
        var services = new ServiceCollection();
        foreach (IServicePlugin servicePlugin in servicePlugins)
        {
            services.AddSingleton(servicePlugin);
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public void GetServicePlugins_ReturnsAllPluginsFromContainer()
    {
        // Arrange
        var servicePlugin = Substitute.For<IServicePlugin>();
        _pluginServiceProvider.GetService(Arg.Is(typeof(IEnumerable<IServicePlugin>)))
            .Returns(new List<IServicePlugin>() { servicePlugin });
        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);

        // Act
        var plugins = runner.GetServicePlugins();

        // Assert
        Assert.Single(plugins);
    }

    [Fact]
    public async Task StartAsync_ShouldLogError_WhenPluginStartFails()
    {
        // Arrange
        var faultyPlugin = Substitute.For<IServicePlugin>();
        faultyPlugin.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("boom")));
        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);

        // Act
        await runner.StartAsync([faultyPlugin], CancellationToken.None);

        // Assert
        _logger.Received(1).LogError(Arg.Any<Exception?>(), "Failed to start service plug-in.");
    }

    [Fact]
    public async Task StopAsync_ShouldLogError_WhenPluginStopFails()
    {
        // Arrange
        var faultyPlugin = Substitute.For<IServicePlugin>();
        faultyPlugin.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("boom")));
        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);

        // Act
        await runner.StopAsync([faultyPlugin], CancellationToken.None);

        // Assert
        _logger.Received(1).LogError(Arg.Any<Exception?>(), "Failed to stop service plug-in.");
    }

    [Fact]
    public async Task StartAsync_ShouldNotContainStoppedPlugin_WhenPluginStartFails()
    {
        // Arrange
        var faultyPlugin = Substitute.For<IServicePlugin>();
        var okPlugin = Substitute.For<IServicePlugin>();
        faultyPlugin.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("boom")));
        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);

        // Act
        var stopped = await runner.StopAsync([faultyPlugin, okPlugin], CancellationToken.None);

        // Assert
        Assert.DoesNotContain(faultyPlugin, stopped);
        Assert.Contains(okPlugin, stopped);
    }

    [Theory]
    [InlineData("StartingAsync")]
    [InlineData("StartedAsync")]
    [InlineData("StoppingAsync")]
    [InlineData("StoppedAsync")]
    public async Task LifecyclePhase_ShouldLogErrorAndContinue_WhenPluginThrows(string phaseName)
    {
        // Arrange
        var failingPlugin = Substitute.For<ILifecycleServicePlugin>();
        var succeedingPlugin = Substitute.For<ILifecycleServicePlugin>();

        SetupPhaseToThrow(failingPlugin, phaseName);

        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);
        IEnumerable<IServicePlugin> plugins = [failingPlugin, succeedingPlugin];

        // Act
        await InvokePhase(runner, plugins, phaseName);

        // Assert
        await VerifySucceedingPluginCalled(succeedingPlugin, phaseName);
    }

    private static void SetupPhaseToThrow(ILifecycleServicePlugin plugin, string phaseName)
    {
        switch (phaseName)
        {
            case "StartingAsync": plugin.StartingAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("error"))); break;
            case "StartedAsync": plugin.StartedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("error"))); break;
            case "StoppingAsync": plugin.StoppingAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("error"))); break;
            case "StoppedAsync": plugin.StoppedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("error"))); break;
        }
    }

    private static Task InvokePhase(IServicePluginLifecycleRunner runner, IEnumerable<IServicePlugin> plugins, string phaseName)
        => phaseName switch
        {
            "StartingAsync" => runner.StartingAsync(plugins, CancellationToken.None),
            "StartedAsync" => runner.StartedAsync(plugins, CancellationToken.None),
            "StoppingAsync" => runner.StoppingAsync(plugins, CancellationToken.None),
            "StoppedAsync" => runner.StoppedAsync(plugins, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(phaseName))
        };

    private static async Task VerifySucceedingPluginCalled(ILifecycleServicePlugin succeedingPlugin, string phaseName)
    {
        switch (phaseName)
        {
            case "StartingAsync": await succeedingPlugin.Received(1).StartingAsync(Arg.Any<CancellationToken>()); break;
            case "StartedAsync": await succeedingPlugin.Received(1).StartedAsync(Arg.Any<CancellationToken>()); break;
            case "StoppingAsync": await succeedingPlugin.Received(1).StoppingAsync(Arg.Any<CancellationToken>()); break;
            case "StoppedAsync": await succeedingPlugin.Received(1).StoppedAsync(Arg.Any<CancellationToken>()); break;
        }
    }
}
