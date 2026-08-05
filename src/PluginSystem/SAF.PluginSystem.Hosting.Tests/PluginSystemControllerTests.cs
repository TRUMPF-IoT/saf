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
    private readonly ILogger<ServicePluginLifecycleRunner> _runnerLogger = Substitute.For<ILogger<ServicePluginLifecycleRunner>>();
    private readonly IPluginServicesContainer _pluginServicesContainer = Substitute.For<IPluginServicesContainer>();
    private readonly IPluginServicesReloader _pluginServicesReloader = Substitute.For<IPluginServicesReloader>();
    private IServicePluginLifecycleRunner LifecycleRunner => new ServicePluginLifecycleRunner(_runnerLogger, _pluginServicesContainer);

    public PluginSystemControllerTests()
    {
        _pluginServicesContainer.IsInitialized.Returns(true);
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
    public async Task ReloadAsync_WhenContainerNotInitialized_ThrowsInvalidOperationException()
    {
        // Arrange
        _pluginServicesContainer.IsInitialized.Returns(false);
        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        var exception = await Record.ExceptionAsync(() => controller.ReloadAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        await _pluginServicesReloader.DidNotReceive().ReinitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReloadAsync_ExecutesLifecyclePhasesAndReinitializesServicePlugins_InOrder()
    {
        // Arrange
        var servicePlugin = Substitute.For<ILifecycleServicePlugin>();
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(servicePlugin)]);
        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        await controller.ReloadAsync(TestContext.Current.CancellationToken);

        // Assert
        Received.InOrder(() =>
        {
            servicePlugin.StoppingAsync(Arg.Any<CancellationToken>());
            servicePlugin.StopAsync(Arg.Any<CancellationToken>());
            servicePlugin.StoppedAsync(Arg.Any<CancellationToken>());
            _pluginServicesReloader.ReinitializeAsync(Arg.Any<CancellationToken>());
            servicePlugin.StartingAsync(Arg.Any<CancellationToken>());
            servicePlugin.StartAsync(Arg.Any<CancellationToken>());
            servicePlugin.StartedAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ReloadAsync_UsesLinkedTokens_ForPluginLifecycleAndStartStopCallbacks()
    {
        // Arrange
        var servicePlugin = Substitute.For<ILifecycleServicePlugin>();
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(servicePlugin)]);
        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Act
        await controller.ReloadAsync(cancellationToken);

        // Assert
        await servicePlugin.Received(1).StoppingAsync(Arg.Is<CancellationToken>(ct => ct != cancellationToken));
        await servicePlugin.Received(1).StopAsync(Arg.Is<CancellationToken>(ct => ct != cancellationToken));
        await servicePlugin.Received(1).StoppedAsync(Arg.Is<CancellationToken>(ct => ct != cancellationToken));

        await servicePlugin.Received(1).StartingAsync(Arg.Is<CancellationToken>(ct => ct != cancellationToken));
        await servicePlugin.Received(1).StartAsync(Arg.Is<CancellationToken>(ct => ct != cancellationToken));
        await servicePlugin.Received(1).StartedAsync(Arg.Is<CancellationToken>(ct => ct != cancellationToken));
    }

    [Fact]
    public async Task ReloadAsync_Reinitializes_WhenNoServicePluginsPresent()
    {
        // Arrange
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith()]);
        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        await controller.ReloadAsync(TestContext.Current.CancellationToken);

        // Assert
        await _pluginServicesReloader.Received(1).ReinitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReloadAsync_SwallowsPluginStopException_AndStillReinitializes()
    {
        // Arrange
        var faultyPlugin = Substitute.For<IServicePlugin>();
        faultyPlugin.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("boom")));
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(faultyPlugin)]);
        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        var exception = await Record.ExceptionAsync(() => controller.ReloadAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
        await _pluginServicesReloader.Received(1).ReinitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReloadAsync_SwallowsPluginStartException()
    {
        // Arrange
        var faultyPlugin = Substitute.For<IServicePlugin>();
        faultyPlugin.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("boom")));
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(faultyPlugin)]);
        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        var exception = await Record.ExceptionAsync(() => controller.ReloadAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
        await faultyPlugin.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReloadAsync_WhenReinitializeFails_RestartsStoppedPluginsAndRethrows()
    {
        // Arrange
        var servicePlugin = Substitute.For<IServicePlugin>();
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(servicePlugin)]);
        _pluginServicesReloader.ReinitializeAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new InvalidOperationException("boom")));
        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        var exception = await Record.ExceptionAsync(() => controller.ReloadAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Received.InOrder(() =>
        {
            servicePlugin.StopAsync(Arg.Any<CancellationToken>());
            _pluginServicesReloader.ReinitializeAsync(Arg.Any<CancellationToken>());
            servicePlugin.StartAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ReloadAsync_WhenStopIsCanceled_AbortsRemainingStopOperationsAndRethrows()
    {
        // Arrange
        var firstPlugin = Substitute.For<IServicePlugin>();
        var secondPlugin = Substitute.For<IServicePlugin>();
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(firstPlugin, secondPlugin)]);

        using var cancellationTokenSource = new CancellationTokenSource();
        firstPlugin.StopAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellationTokenSource.Cancel();
                return Task.FromCanceled(cancellationTokenSource.Token);
            });

        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        var exception = await Record.ExceptionAsync(() => controller.ReloadAsync(cancellationTokenSource.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        await secondPlugin.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
        await _pluginServicesReloader.DidNotReceive().ReinitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReloadAsync_WhenStartIsCanceled_AbortsRemainingStartOperationsAndRethrows()
    {
        // Arrange
        var firstPlugin = Substitute.For<IServicePlugin>();
        var secondPlugin = Substitute.For<IServicePlugin>();
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(firstPlugin, secondPlugin)]);

        using var cancellationTokenSource = new CancellationTokenSource();
        firstPlugin.StartAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellationTokenSource.Cancel();
                return Task.FromCanceled(cancellationTokenSource.Token);
            });

        secondPlugin.StartAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                return token.IsCancellationRequested
                    ? Task.FromException(new InvalidOperationException("Second plugin should not receive a canceled token."))
                    : Task.CompletedTask;
            });

        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        var exception = await Record.ExceptionAsync(() => controller.ReloadAsync(cancellationTokenSource.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        await secondPlugin.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await _pluginServicesReloader.Received(1).ReinitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReloadAsync_WhenReinitializeIsCanceled_RestartsStoppedPluginsAndRethrows()
    {
        // Arrange
        var servicePlugin = Substitute.For<IServicePlugin>();
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(servicePlugin)]);

        using var cancellationTokenSource = new CancellationTokenSource();
        _pluginServicesReloader.ReinitializeAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellationTokenSource.Cancel();
                return ValueTask.FromCanceled(cancellationTokenSource.Token);
            });

        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        var exception = await Record.ExceptionAsync(() => controller.ReloadAsync(cancellationTokenSource.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Received.InOrder(() =>
        {
            servicePlugin.StopAsync(Arg.Any<CancellationToken>());
            _pluginServicesReloader.ReinitializeAsync(Arg.Any<CancellationToken>());
            servicePlugin.StartAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ReloadAsync_WhenLifecyclePhaseThrows_ContinuesReload()
    {
        // Arrange
        var servicePlugin = Substitute.For<ILifecycleServicePlugin>();
        servicePlugin.StoppingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith(servicePlugin)]);

        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        var exception = await Record.ExceptionAsync(() => controller.ReloadAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
        await servicePlugin.Received(1).StoppingAsync(Arg.Any<CancellationToken>());
        await servicePlugin.Received(1).StopAsync(Arg.Any<CancellationToken>());
        await servicePlugin.Received(1).StoppedAsync(Arg.Any<CancellationToken>());
        await servicePlugin.Received(1).StartingAsync(Arg.Any<CancellationToken>());
        await servicePlugin.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await servicePlugin.Received(1).StartedAsync(Arg.Any<CancellationToken>());
        await _pluginServicesReloader.Received(1).ReinitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReloadAsync_WhenCalledConcurrently_SerializesReloadOperations()
    {
        // Arrange
        _pluginServicesContainer.GetPluginServices().Returns([BuildProviderWith()]);

        var firstReloadEnteredReinitialize = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstReloadToContinue = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondReloadEnteredReinitialize = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reinitializeCallCount = 0;

        _pluginServicesReloader.ReinitializeAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var callNumber = Interlocked.Increment(ref reinitializeCallCount);
                if (callNumber == 1)
                {
                    firstReloadEnteredReinitialize.TrySetResult();
                    return new ValueTask(allowFirstReloadToContinue.Task);
                }

                secondReloadEnteredReinitialize.TrySetResult();
                return ValueTask.CompletedTask;
            });

        var controller = new PluginSystemController(_logger, _pluginServicesContainer, _pluginServicesReloader, LifecycleRunner);

        // Act
        var firstReloadTask = controller.ReloadAsync(TestContext.Current.CancellationToken);
        await firstReloadEnteredReinitialize.Task;

        var secondReloadTask = controller.ReloadAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(secondReloadEnteredReinitialize.Task.IsCompleted);

        allowFirstReloadToContinue.TrySetResult();
        await Task.WhenAll(firstReloadTask, secondReloadTask);

        Assert.True(secondReloadEnteredReinitialize.Task.IsCompleted);
        await _pluginServicesReloader.Received(2).ReinitializeAsync(Arg.Any<CancellationToken>());
    }
}

