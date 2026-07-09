// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting.Extensions.Tests;

using Contracts;
using Hosting.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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