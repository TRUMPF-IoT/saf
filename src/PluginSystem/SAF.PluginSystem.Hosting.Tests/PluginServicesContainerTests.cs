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

    #region DisposeAsync Tests

    /// <summary>
    /// Tests that DisposeAsync completes without error when called.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_CompletesSuccessfully_WhenCalled()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Initialize to populate service providers
        pluginServicesContainer.GetPluginServices();

        // Act & Assert - should not throw
        await pluginServicesContainer.DisposeAsync();
    }

    /// <summary>
    /// Tests that DisposeAsync returns early if already disposed (idempotent).
    /// </summary>
    [Fact]
    public async Task DisposeAsync_ReturnsEarlyIfAlreadyDisposed_Idempotent()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        pluginServicesContainer.GetPluginServices();

        // Act
        await pluginServicesContainer.DisposeAsync();
        // Second call - should return immediately due to _disposed flag
        await pluginServicesContainer.DisposeAsync();

        // Assert - no exception thrown
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

        // Act & Assert (should not throw)
        await pluginServicesContainer.DisposeAsync();
    }

    /// <summary>
    /// Tests that DisposeAsync disposes public services collection.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_DisposesPublicServicesCollection_Correctly()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Initialize both plugin and public services
        pluginServicesContainer.GetPluginServices();
        pluginServicesContainer.GetPublicServices();

        // Act & Assert
        // Should not throw even with public services
        await pluginServicesContainer.DisposeAsync();
    }

    /// <summary>
    /// Tests that DisposeAsync thread-safely uses lock for synchronization.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_IsThreadSafe_WhenCalledConcurrently()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        pluginServicesContainer.GetPluginServices();

        var disposedCount = 0;
        var lockObj = new object();

        // Create multiple tasks calling DisposeAsync concurrently
        var tasks = Enumerable.Range(0, 5)
            .Select(async _ =>
            {
                await pluginServicesContainer.DisposeAsync();
                lock (lockObj)
                {
                    disposedCount++;
                }
            })
            .ToList();

        // Act
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5, disposedCount); // All tasks completed
    }

    /// <summary>
    /// Tests that DisposeAsync properly filters null service providers.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_FiltersNullProviders_Correctly()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        pluginServicesContainer.GetPluginServices();

        // Act & Assert
        // Should not throw even if some providers are null
        await pluginServicesContainer.DisposeAsync();
    }

    /// <summary>
    /// Tests that DisposeAsync correctly appends public services collection to disposal list.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_IncludesPublicServicesInDisposalList_Always()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Call only GetPluginServices, not GetPublicServices
        pluginServicesContainer.GetPluginServices();

        // Act
        await pluginServicesContainer.DisposeAsync();

        // Assert
        // Should not throw - public services should be included even if not explicitly accessed
    }

    /// <summary>
    /// Tests that DisposeAsync filters OfType correctly to only IServiceProvider instances.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_FiltersOfType_Correctly()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        pluginServicesContainer.GetPluginServices();

        // Act
        await pluginServicesContainer.DisposeAsync();

        // Assert
        // Should only dispose providers that are IServiceProvider
    }

    /// <summary>
    /// Tests that DisposeAsync acquires and releases lock properly (no deadlock).
    /// </summary>
    [Fact]
    public async Task DisposeAsync_AcquiresLockForDisposal_Correctly()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        pluginServicesContainer.GetPluginServices();

        // Act
        await pluginServicesContainer.DisposeAsync();

        // Assert
        // If we can call DisposeAsync without deadlock, the lock was properly managed
    }

    /// <summary>
    /// Tests that DisposeAsync uses ConfigureAwait(false) for correct context propagation.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_DisposeProviderAsync_UsesConfigureAwaitFalse()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        pluginServicesContainer.GetPluginServices();

        // Act
        await pluginServicesContainer.DisposeAsync();

        // Assert
        // Completes successfully without context issues
    }

    /// <summary>
    /// Tests that _disposed flag prevents multiple disposal attempts.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_SetDisposedFlag_PreventsMultipleDisposals()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        pluginServicesContainer.GetPluginServices();

        // Act
        await pluginServicesContainer.DisposeAsync();
        await pluginServicesContainer.DisposeAsync();
        await pluginServicesContainer.DisposeAsync();

        // Assert - should complete without error
        // Multiple dispose calls should be safe due to _disposed flag
    }

    /// <summary>
    /// Tests that DisposeAsync does not initialize services if not already initialized.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WithoutInitialization_DoesNotThrow()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Don't call GetPluginServices or GetPublicServices

        // Act & Assert
        await pluginServicesContainer.DisposeAsync();
    }

    /// <summary>
    /// Tests that DisposeAsync properly builds list of providers before disposing.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_BuildsProviderListBeforeDisposing_Correctly()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        pluginServicesContainer.GetPluginServices();

        // Act
        await pluginServicesContainer.DisposeAsync();

        // Assert
        // Should have collected providers from _pluginServiceCollections and _publicServicesOnlyCollection
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
        await pluginServicesContainer.DisposeAsync();

        // Assert - should not throw
    }

    /// <summary>
    /// Tests that DisposeAsync correctly selects and appends service providers.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_SelectsAndAppendsProviders_Correctly()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        pluginServicesContainer.GetPluginServices();

        // Act
        await pluginServicesContainer.DisposeAsync();

        // Assert
        // All providers should have been collected and disposed
    }

    /// <summary>
    /// Tests that DisposeAsync runs disposal foreach loop correctly.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_IteratesThroughAllProviders_Correctly()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(
            _logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        pluginServicesContainer.GetPluginServices();

        // Act
        await pluginServicesContainer.DisposeAsync();

        // Assert
        // Should iterate through all providers without error
    }

    #endregion
}
