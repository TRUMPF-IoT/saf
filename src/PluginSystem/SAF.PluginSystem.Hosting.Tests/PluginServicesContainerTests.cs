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
}