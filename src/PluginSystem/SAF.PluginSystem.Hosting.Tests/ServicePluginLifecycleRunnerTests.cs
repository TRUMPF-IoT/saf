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
    public async Task StopAsync_ShouldStopRemainingPlugins_WhenPluginStopFails()
    {
        // Arrange
        var faultyPlugin = Substitute.For<IServicePlugin>();
        var okPlugin = Substitute.For<IServicePlugin>();
        faultyPlugin.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("boom")));
        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);

        // Act: stopped in reverse order, so the faulty plug-in goes first
        var exception = await Record.ExceptionAsync(() => runner.StopAsync([okPlugin, faultyPlugin], CancellationToken.None));

        // Assert
        Assert.Null(exception);
        await okPlugin.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_ShouldStartRemainingPlugins_WhenPluginStartFails()
    {
        // Arrange
        var faultyPlugin = Substitute.For<IServicePlugin>();
        var okPlugin = Substitute.For<IServicePlugin>();
        faultyPlugin.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("boom")));
        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);

        // Act
        var exception = await Record.ExceptionAsync(() => runner.StartAsync([faultyPlugin, okPlugin], CancellationToken.None));

        // Assert
        Assert.Null(exception);
        await okPlugin.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_ShouldAbortAtNextPlugin_WhenCanceled()
    {
        // Arrange
        var cancelingPlugin = Substitute.For<IServicePlugin>();
        var notReachedPlugin = Substitute.For<IServicePlugin>();

        using var cancellationTokenSource = new CancellationTokenSource();
        cancelingPlugin.StopAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellationTokenSource.Cancel();
                return Task.FromCanceled(cancellationTokenSource.Token);
            });

        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);

        // Act: stopped in reverse order, so the canceling plug-in goes first
        var exception = await Record.ExceptionAsync(
            () => runner.StopAsync([notReachedPlugin, cancelingPlugin], cancellationTokenSource.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        await notReachedPlugin.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LifecyclePhase_ShouldAbortAtNextPlugin_WhenCanceled()
    {
        // Arrange
        var cancelingPlugin = Substitute.For<ILifecycleServicePlugin>();
        var notReachedPlugin = Substitute.For<ILifecycleServicePlugin>();

        using var cancellationTokenSource = new CancellationTokenSource();
        cancelingPlugin.StoppingAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellationTokenSource.Cancel();
                return Task.FromCanceled(cancellationTokenSource.Token);
            });

        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);

        // Act: stopping runs in reverse order, so the canceling plug-in goes first
        var exception = await Record.ExceptionAsync(
            () => runner.StoppingAsync([notReachedPlugin, cancelingPlugin], cancellationTokenSource.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        await notReachedPlugin.DidNotReceive().StoppingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LifecyclePhase_ShouldOnlyRunOnLifecyclePlugins()
    {
        // Arrange
        var lifecyclePlugin = Substitute.For<ILifecycleServicePlugin>();
        var plainPlugin = Substitute.For<IServicePlugin>();
        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);

        // Act
        await runner.StoppedAsync([plainPlugin, lifecyclePlugin], CancellationToken.None);

        // Assert
        await lifecyclePlugin.Received(1).StoppedAsync(Arg.Any<CancellationToken>());
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

        // The stop phases run in reverse order; either way the failing plug-in goes first.
        IEnumerable<IServicePlugin> plugins = phaseName.StartsWith("Stop", StringComparison.Ordinal)
            ? [succeedingPlugin, failingPlugin]
            : [failingPlugin, succeedingPlugin];

        // Act
        await InvokePhase(runner, plugins, phaseName);

        // Assert
        await VerifySucceedingPluginCalled(succeedingPlugin, phaseName);
    }

    /// <summary>
    /// Tests that the start phases run over the plug-ins in the order they are passed in, and the stop phases in
    /// the reverse order, as the .NET host does with its hosted services.
    /// </summary>
    [Theory]
    [InlineData("StartingAsync", "first", "second")]
    [InlineData("StartAsync", "first", "second")]
    [InlineData("StartedAsync", "first", "second")]
    [InlineData("StoppingAsync", "second", "first")]
    [InlineData("StopAsync", "second", "first")]
    [InlineData("StoppedAsync", "second", "first")]
    public async Task Phase_ShouldRunStartPhasesInOrder_AndStopPhasesInReverse(
        string phaseName, string expectedFirst, string expectedSecond)
    {
        // Arrange
        List<string> calls = [];
        var runner = new ServicePluginLifecycleRunner(_logger, _pluginServicesContainer);
        IEnumerable<IServicePlugin> plugins = [new RecordingPlugin("first", calls), new RecordingPlugin("second", calls)];

        // Act
        await InvokePhase(runner, plugins, phaseName);

        // Assert
        Assert.Equal([expectedFirst, expectedSecond], calls);
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
            "StartAsync" => runner.StartAsync(plugins, CancellationToken.None),
            "StartedAsync" => runner.StartedAsync(plugins, CancellationToken.None),
            "StoppingAsync" => runner.StoppingAsync(plugins, CancellationToken.None),
            "StopAsync" => runner.StopAsync(plugins, CancellationToken.None),
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

    /// <summary>
    /// Records its name in <paramref name="calls"/> in every lifecycle phase.
    /// </summary>
    private sealed class RecordingPlugin(string name, List<string> calls) : ILifecycleServicePlugin
    {
        public Task StartingAsync(CancellationToken cancellationToken) => Record();

        public Task StartAsync(CancellationToken token) => Record();

        public Task StartedAsync(CancellationToken cancellationToken) => Record();

        public Task StoppingAsync(CancellationToken cancellationToken) => Record();

        public Task StopAsync(CancellationToken token) => Record();

        public Task StoppedAsync(CancellationToken cancellationToken) => Record();

        private Task Record()
        {
            calls.Add(name);
            return Task.CompletedTask;
        }
    }
}
