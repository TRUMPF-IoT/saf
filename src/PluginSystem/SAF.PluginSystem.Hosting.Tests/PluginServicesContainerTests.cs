// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.Logging;
using NSubstitute;

public class PluginServicesContainerTests
{
    private readonly ILogger<PluginServicesContainer> _logger;
    private readonly IPluginSystemHostContext _hostContext;
    private readonly IPluginManifest _pluginManifest;
    private readonly IPluginAssemblyContainer _pluginContainer;
    private readonly IServiceProvider _applicationServiceProvider;
    private readonly IPublicServiceTypeRegistry _publicServiceTypeRegistry;

    public PluginServicesContainerTests()
    {
        _logger = Substitute.For<ILogger<PluginServicesContainer>>();
        _hostContext = Substitute.For<IPluginSystemHostContext>();
        _applicationServiceProvider = Substitute.For<IServiceProvider>();
        _publicServiceTypeRegistry = Substitute.For<IPublicServiceTypeRegistry>();

        _pluginManifest = Substitute.For<IPluginManifest>();
        List<IPluginManifest> pluginManifests = [_pluginManifest];

        _pluginContainer = Substitute.For<IPluginAssemblyContainer>();
        _pluginContainer.GetPluginManifests().Returns(pluginManifests);
    }

    private static Task DisposeContainerAsync(PluginServicesContainer pluginServicesContainer) =>
        pluginServicesContainer.DisposeAsync().AsTask();

    [Fact]
    public async Task GetPluginServices_ShouldInitializePlugins_WhenCalled()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var result = pluginServicesContainer.GetPluginServices();

        // Assert
        Assert.NotEmpty(result);
        _pluginContainer.Received(1).GetPluginManifests();
        _logger.Received().LogInformation("Starting plug-in search and initialization.");
    }

    [Fact]
    public async Task GetPublicServices_ShouldInitializePlugins_WhenCalled()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var result = pluginServicesContainer.GetPublicServices();

        // Assert
        Assert.NotNull(result);
        _pluginContainer.Received(1).GetPluginManifests();
        _logger.Received().LogInformation("Starting plug-in search and initialization.");
    }

    [Fact]
    public async Task GetPluginServices_ShouldReturnCachedServices_WhenCalledMultipleTimes()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var result1 = pluginServicesContainer.GetPluginServices();

        _pluginContainer.ClearReceivedCalls();
        _logger.ClearReceivedCalls();

        var result2 = pluginServicesContainer.GetPluginServices();

        // Assert
        Assert.True(result1.SequenceEqual(result2));
        _pluginContainer.DidNotReceive().GetPluginManifests();
        _logger.DidNotReceive().LogInformation("Starting plug-in search and initialization.");
    }

    [Fact]
    public async Task GetPublicServices_ShouldReturnCachedServices_WhenCalledMultipleTimes()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var result1 = pluginServicesContainer.GetPublicServices();

        _pluginContainer.ClearReceivedCalls();
        _logger.ClearReceivedCalls();

        var result2 = pluginServicesContainer.GetPublicServices();

        // Assert
        Assert.Equal(result1, result2);
        _pluginContainer.DidNotReceive().GetPluginManifests();
        _logger.DidNotReceive().LogInformation("Starting plug-in search and initialization.");
    }

    /// <summary>
    /// Tests that DisposeAsync does not throw for supported initialization scenarios.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetDisposeAsyncNoThrowScenarios))]
    public async Task DisposeAsync_DoesNotThrow_ForSupportedScenarios(Action<PluginServicesContainer> setup)
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);
        setup(pluginServicesContainer);

        // Act
        var exception = await Record.ExceptionAsync(() => DisposeContainerAsync(pluginServicesContainer));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that DisposeAsync remains idempotent when called repeatedly.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task DisposeAsync_DoesNotThrow_WhenCalledMultipleTimes(int disposeCalls)
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);
        pluginServicesContainer.GetPluginServices();

        // Act
        var exceptions = new List<Exception?>();
        for (var i = 0; i < disposeCalls; i++)
        {
            exceptions.Add(await Record.ExceptionAsync(() => DisposeContainerAsync(pluginServicesContainer)));
        }

        // Assert
        Assert.All(exceptions, Assert.Null);
    }

    /// <summary>
    /// Tests that DisposeAsync handles empty service collections gracefully.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_HandlesEmptyServiceCollections_Gracefully()
    {
        // Arrange
        var emptyPluginContainer = Substitute.For<IPluginAssemblyContainer>();
        emptyPluginContainer.GetPluginManifests().Returns([]);

        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [emptyPluginContainer], _publicServiceTypeRegistry);
        pluginServicesContainer.GetPluginServices();

        // Act
        var exception = await Record.ExceptionAsync(() => DisposeContainerAsync(pluginServicesContainer));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests DisposeAsync with multiple plugin containers.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WithMultiplePluginContainers_DisposesAll()
    {
        // Arrange
        var pluginContainer1 = Substitute.For<IPluginAssemblyContainer>();
        var pluginContainer2 = Substitute.For<IPluginAssemblyContainer>();
        var manifest1 = Substitute.For<IPluginManifest>();
        var manifest2 = Substitute.For<IPluginManifest>();

        pluginContainer1.GetPluginManifests().Returns([manifest1]);
        pluginContainer2.GetPluginManifests().Returns([manifest2]);

        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [pluginContainer1, pluginContainer2], _publicServiceTypeRegistry);
        pluginServicesContainer.GetPluginServices();

        // Act
        var exception = await Record.ExceptionAsync(() => DisposeContainerAsync(pluginServicesContainer));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that DisposeAsync is thread-safe when invoked concurrently.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_IsThreadSafe_WhenCalledConcurrently()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);
        pluginServicesContainer.GetPluginServices();

        // Act
        var disposeTasks = Enumerable.Range(0, 5)
            .Select(_ => Record.ExceptionAsync(() => DisposeContainerAsync(pluginServicesContainer)).AsTask());
        var exceptions = await Task.WhenAll(disposeTasks);

        // Assert
        Assert.All(exceptions, Assert.Null);
    }

    /// <summary>
    /// Tests that ReinitializeAsync re-runs the plugin manifests and swaps in fresh service providers.
    /// </summary>
    [Fact]
    public async Task ReinitializeAsync_RebuildsProviders_AndReenumeratesManifests()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);
        var providerBefore = pluginServicesContainer.GetPublicServices();
        _pluginContainer.ClearReceivedCalls();

        // Act
        await pluginServicesContainer.ReinitializeAsync();
        var providerAfter = pluginServicesContainer.GetPublicServices();

        // Assert
        Assert.NotSame(providerBefore, providerAfter);
        _pluginContainer.Received(1).GetPluginManifests();
    }

    /// <summary>
    /// Tests that ReinitializeAsync disposes the previously built service providers.
    /// </summary>
    [Fact]
    public async Task ReinitializeAsync_DisposesPreviousProviders()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);
        var providerBefore = pluginServicesContainer.GetPublicServices();

        // Act
        await pluginServicesContainer.ReinitializeAsync();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => providerBefore.GetService(typeof(IServiceProvider)));
    }

    /// <summary>
    /// Tests that ReinitializeAsync builds providers even when no prior initialization happened.
    /// </summary>
    [Fact]
    public async Task ReinitializeAsync_WithoutPriorInitialization_BuildsProviders()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        await pluginServicesContainer.ReinitializeAsync();

        // Assert
        Assert.NotNull(pluginServicesContainer.GetPublicServices());
        Assert.NotEmpty(pluginServicesContainer.GetPluginServices());
        _pluginContainer.Received().GetPluginManifests();
    }

    /// <summary>
    /// Tests that repeated reinitializations keep working and do not leak the public-services collection.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task ReinitializeAsync_CalledMultipleTimes_KeepsProvidersUsable(int reloads)
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);
        pluginServicesContainer.GetPublicServices();

        // Act
        for (var i = 0; i < reloads; i++)
        {
            await pluginServicesContainer.ReinitializeAsync();
        }

        // Assert
        Assert.NotNull(pluginServicesContainer.GetPublicServices());
    }

    /// <summary>
    /// Tests that ReinitializeAsync throws once the container has been disposed.
    /// </summary>
    [Fact]
    public async Task ReinitializeAsync_AfterDispose_Throws()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);
        pluginServicesContainer.GetPluginServices();
        await DisposeContainerAsync(pluginServicesContainer);

        // Act + Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await pluginServicesContainer.ReinitializeAsync());
    }

    /// <summary>
    /// Tests that ReinitializeAsync honors a cancellation request.
    /// </summary>
    [Fact]
    public async Task ReinitializeAsync_WithCancelledToken_Throws()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);
        pluginServicesContainer.GetPluginServices();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act + Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await pluginServicesContainer.ReinitializeAsync(cts.Token));
    }

    /// <summary>
    /// Provides setup scenarios for DisposeAsync no-throw tests.
    /// </summary>
    public static IEnumerable<object[]> GetDisposeAsyncNoThrowScenarios()
    {        yield return [new Action<PluginServicesContainer>(container =>
        {
            container.GetPluginServices();
        })];

        yield return [new Action<PluginServicesContainer>(container =>
        {
            container.GetPluginServices();
            container.GetPublicServices();
        })];

        yield return [new Action<PluginServicesContainer>(_ =>
        {
            // Intentionally left blank: verify dispose without initialization.
        })];
    }
}
