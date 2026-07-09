// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Extensions.Tests;

using Contracts;
using Hosting.Extensions;
using NSubstitute;

public class PluginServiceProviderExtensionsTests
{
    [Fact]
    public void GetRequiredService_ShouldReturnService_WhenServiceExists()
    {
        // Arrange
        var serviceProvider = Substitute.For<IPluginServiceProvider>();
        var service = new object();
        serviceProvider.GetService<object>().Returns(service);

        // Act
        var result = serviceProvider.GetRequiredService<object>();

        // Assert
        Assert.Equal(service, result);
    }

    [Fact]
    public void GetRequiredService_ShouldThrowInvalidOperationException_WhenServiceDoesNotExist()
    {
        // Arrange
        var serviceProvider = Substitute.For<IPluginServiceProvider>();
        serviceProvider.GetService<object>().Returns(null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => serviceProvider.GetRequiredService<object>());
    }

    [Fact]
    public void GetRequiredKeyedService_ShouldReturnService_WhenKeyedServiceExists()
    {
        // Arrange
        var serviceProvider = Substitute.For<IPluginServiceProvider>();
        var service = new object();
        serviceProvider.GetKeyedService<object>("key").Returns(service);

        // Act
        var result = serviceProvider.GetRequiredKeyedService<object>("key");

        // Assert
        Assert.Equal(service, result);
    }

    [Fact]
    public void GetRequiredKeyedService_ShouldThrowInvalidOperationException_WhenKeyedServiceDoesNotExist()
    {
        // Arrange
        var serviceProvider = Substitute.For<IPluginServiceProvider>();
        serviceProvider.GetKeyedService<object>("key").Returns(null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => serviceProvider.GetRequiredKeyedService<object>("key"));
    }
}