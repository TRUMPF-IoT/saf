// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

public class SharedServicesRegistryExtensionsTests
{
    private readonly ISharedServiceRegistry _registry = new SharedServiceRegistry();

    [Fact]
    public void RedirectServicesRegistersSingletonService()
    {
        // Arrange
        var testService = Substitute.For<IDummyService>();
        _registry.Services.AddSingleton(_ => testService);
        using var sourceProvider = _registry.Services.BuildServiceProvider();

        var target = new ServiceCollection();

        // Act
        _registry.RedirectServices(sourceProvider, target);

        // Assert
        using var serviceProvider = target.BuildServiceProvider();
        var resolvedService = serviceProvider.GetRequiredService<IDummyService>();
        Assert.Same(testService, resolvedService);
    }

    [Fact]
    public void RedirectServicesRegistersTransientService()
    {
        // Arrange
        _registry.Services.AddTransient(_ => Substitute.For<IDummyService>());
        using var sourceProvider = _registry.Services.BuildServiceProvider();

        var target = new ServiceCollection();

        // Act
        _registry.RedirectServices(sourceProvider, target);

        // Assert
        using var serviceProvider = target.BuildServiceProvider();

        var resolvedService1 = serviceProvider.GetRequiredService<IDummyService>();
        var resolvedService2 = serviceProvider.GetRequiredService<IDummyService>();
        Assert.NotSame(resolvedService1, resolvedService2);
    }

    [Fact]
    public void RedirectServicesThrowsInvalidOperationExceptionForScopedServices()
    {
        // Arrange
        _registry.Services.AddScoped(_ => Substitute.For<IDummyService>());
        using var sourceProvider = _registry.Services.BuildServiceProvider();

        var target = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _registry.RedirectServices(sourceProvider, target));
        Assert.Contains("Scoped service is not supported", ex.Message);
    }

    public interface IDummyService;
}