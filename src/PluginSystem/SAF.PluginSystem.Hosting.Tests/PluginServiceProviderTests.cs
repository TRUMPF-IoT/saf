// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public interface IDummyService;

public class PluginServiceProviderTests
{
    [Fact]
    public void GetService_ShouldReturnSingleService_WhenServiceExists()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        var service = Substitute.For<IDummyService>();
        serviceProvider.GetService(Arg.Is(typeof(IEnumerable<IDummyService>))).Returns(new List<IDummyService> { service });

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act
        var result = pluginServiceProvider.GetService<IDummyService>();

        // Assert
        Assert.Equal(service, result);
    }

    [Fact]
    public void GetService_ShouldReturnNull_WhenServiceDoesNotExist()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(Arg.Is(typeof(IEnumerable<IDummyService>))).Returns(new List<IDummyService>());

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act
        var result = pluginServiceProvider.GetService<IDummyService>();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetKeyedService_ShouldReturnSingleService_WhenKeyedServiceExists()
    {
        // Arrange
        var serviceProvider = Substitute.For<IKeyedServiceProvider>();
        var service = Substitute.For<IDummyService>();
        serviceProvider.GetRequiredKeyedService(Arg.Is(typeof(IEnumerable<IDummyService>)), Arg.Is("key")).Returns(new List<IDummyService> { service });

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act
        var result = pluginServiceProvider.GetKeyedService<IDummyService>("key");

        // Assert
        Assert.Equal(service, result);
    }

    [Fact]
    public void GetKeyedService_ShouldReturnNull_WhenKeyedServiceDoesNotExist()
    {
        // Arrange
        var serviceProvider = Substitute.For<IKeyedServiceProvider>();
        serviceProvider.GetRequiredKeyedService(Arg.Is(typeof(IEnumerable<IDummyService>)), Arg.Is("key")).Returns(new List<IDummyService>());

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act
        var result = pluginServiceProvider.GetKeyedService<IDummyService>("key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetServices_ShouldReturnAllServices_WhenServicesExist()
    {
        // Arrange
        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var service1 = Substitute.For<IDummyService>();
        var service2 = Substitute.For<IDummyService>();
        serviceProvider.GetService(Arg.Is(typeof(IEnumerable<IDummyService>))).Returns(new List<IDummyService> { service1, service2 });
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act
        var result = pluginServiceProvider.GetServices<IDummyService>().ToList();

        // Assert
        Assert.Contains(service1, result);
        Assert.Contains(service2, result);
    }

    [Fact]
    public void GetKeyedServices_ShouldReturnAllKeyedServices_WhenKeyedServicesExist()
    {
        // Arrange
        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        var serviceProvider = Substitute.For<IKeyedServiceProvider>();
        var service1 = Substitute.For<IDummyService>();
        var service2 = Substitute.For<IDummyService>();
        serviceProvider.GetRequiredKeyedService(Arg.Is(typeof(IEnumerable<IDummyService>)), Arg.Is("key")).Returns(new List<IDummyService> { service1, service2 });
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act
        var result = pluginServiceProvider.GetKeyedServices<IDummyService>("key").ToList();

        // Assert
        Assert.Contains(service1, result);
        Assert.Contains(service2, result);
    }
}