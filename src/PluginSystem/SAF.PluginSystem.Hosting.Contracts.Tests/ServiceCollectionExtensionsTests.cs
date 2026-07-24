// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts.Tests;

using Microsoft.Extensions.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddServicePlugin_ShouldAddServicePluginToServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddServicePlugin<TestServicePlugin>();

        // Assert
        Assert.Contains(services, s => s.ServiceType == typeof(IServicePlugin) && s.ImplementationType == typeof(TestServicePlugin));
    }

    [Fact]
    public void AddServicePlugin_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddServicePlugin<TestServicePlugin>();

        // Assert
        Assert.Same(services, result);
    }

    private class TestServicePlugin : IServicePlugin
    {
        public Task StartAsync(CancellationToken token) => throw new NotImplementedException();
        public Task StopAsync(CancellationToken token) => throw new NotImplementedException();
    }
}
