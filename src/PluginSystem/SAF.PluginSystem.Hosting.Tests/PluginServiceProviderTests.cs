// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public interface IDummyService;

public struct DummyStruct;

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

    [Fact]
    public void GetRequiredService_ShouldReturnService_WhenServiceExists()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        var service = Substitute.For<IDummyService>();
        serviceProvider.GetService(Arg.Is(typeof(IEnumerable<IDummyService>))).Returns(new List<IDummyService> { service });

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act
        var result = pluginServiceProvider.GetRequiredService<IDummyService>();

        // Assert
        Assert.Equal(service, result);
    }

    [Fact]
    public void GetRequiredService_ShouldThrowInvalidOperationException_WhenServiceDoesNotExist()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(Arg.Is(typeof(IEnumerable<IDummyService>))).Returns(new List<IDummyService>());

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => pluginServiceProvider.GetRequiredService<IDummyService>());
    }

    [Fact]
    public void GetRequiredService_ThrowsInvalidOperationException_WhenValueTypeServiceDoesNotExist()
    {
        // Arrange
        // `GetService<T>() ?? throw` boxes an unconstrained T for the null test; a boxed int is never
        // null, so this used to return 0 instead of throwing.
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(Arg.Is(typeof(IEnumerable<int>))).Returns(new List<int>());

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => pluginServiceProvider.GetRequiredService<int>());
    }

    [Fact]
    public void GetRequiredService_ThrowsInvalidOperationException_WhenStructServiceDoesNotExist()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(Arg.Is(typeof(IEnumerable<DummyStruct>))).Returns(new List<DummyStruct>());

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => pluginServiceProvider.GetRequiredService<DummyStruct>());
    }

    [Fact]
    public void GetRequiredService_ThrowsInvalidOperationException_WithMessageNamingTheType_WhenMultipleServicesRegistered()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(Arg.Is(typeof(IEnumerable<IDummyService>)))
            .Returns(new List<IDummyService> { Substitute.For<IDummyService>(), Substitute.For<IDummyService>() });

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => pluginServiceProvider.GetRequiredService<IDummyService>());
        Assert.Contains(nameof(IDummyService), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRequiredKeyedService_ShouldReturnService_WhenKeyedServiceExists()
    {
        // Arrange
        var serviceProvider = Substitute.For<IKeyedServiceProvider>();
        var service = Substitute.For<IDummyService>();
        serviceProvider.GetRequiredKeyedService(Arg.Is(typeof(IEnumerable<IDummyService>)), Arg.Is("key")).Returns(new List<IDummyService> { service });

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act
        var result = pluginServiceProvider.GetRequiredKeyedService<IDummyService>("key");

        // Assert
        Assert.Equal(service, result);
    }

    [Fact]
    public void GetRequiredKeyedService_ShouldThrowInvalidOperationException_WhenKeyedServiceDoesNotExist()
    {
        // Arrange
        var serviceProvider = Substitute.For<IKeyedServiceProvider>();
        serviceProvider.GetRequiredKeyedService(Arg.Is(typeof(IEnumerable<IDummyService>)), Arg.Is("key")).Returns(new List<IDummyService>());

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => pluginServiceProvider.GetRequiredKeyedService<IDummyService>("key"));
    }

    [Fact]
    public void GetRequiredKeyedService_ThrowsInvalidOperationException_WhenValueTypeServiceDoesNotExist()
    {
        // Arrange
        var serviceProvider = Substitute.For<IKeyedServiceProvider>();
        serviceProvider.GetRequiredKeyedService(Arg.Is(typeof(IEnumerable<int>)), Arg.Is("key")).Returns(new List<int>());

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => pluginServiceProvider.GetRequiredKeyedService<int>("key"));
    }

    [Fact]
    public void GetRequiredKeyedService_ThrowsInvalidOperationException_WithMessageNamingTheTypeAndKey_WhenMultipleServicesRegistered()
    {
        // Arrange
        var serviceProvider = Substitute.For<IKeyedServiceProvider>();
        serviceProvider.GetRequiredKeyedService(Arg.Is(typeof(IEnumerable<IDummyService>)), Arg.Is("key"))
            .Returns(new List<IDummyService> { Substitute.For<IDummyService>(), Substitute.For<IDummyService>() });

        var pluginLoader = Substitute.For<IPluginServicesContainer>();
        pluginLoader.GetPublicServices().Returns(serviceProvider);

        var pluginServiceProvider = new PluginServiceProvider(pluginLoader);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => pluginServiceProvider.GetRequiredKeyedService<IDummyService>("key"));
        Assert.Contains(nameof(IDummyService), exception.Message, StringComparison.Ordinal);
        Assert.Contains("key", exception.Message, StringComparison.Ordinal);
    }
}