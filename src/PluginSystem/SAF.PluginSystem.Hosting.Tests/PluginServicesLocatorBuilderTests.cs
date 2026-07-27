// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.DependencyInjection;

public class PluginServicesLocatorBuilderTests
{
    [Fact]
    public void Build_WhenImportingDisposableSingleton_DoesNotDisposeOwnerInstanceOnImporterDispose()
    {
        var ownerPluginServices = CreateOwnerPluginServices();
        var ownerBuilder = new PluginServicesLocatorBuilder(ownerPluginServices, []);
        ownerBuilder.Build();

        var importingPluginServices = new PluginServiceCollection(new ServiceCollection(), []);
        var importingBuilder = new PluginServicesLocatorBuilder(importingPluginServices, [ownerPluginServices]);
        importingBuilder.Build();

        var importingProvider = importingPluginServices.ServiceProvider!;
        var importedService = importingProvider.GetRequiredService<ITestDisposablePublicService>();

        ((IDisposable)importingProvider).Dispose();

        Assert.Equal(0, importedService.DisposeCallCount);
        Assert.Equal(1, importedService.Increment());

        ((IDisposable)ownerPluginServices.ServiceProvider!).Dispose();
    }

    [Fact]
    public void Build_WhenImportingDisposableSingleton_ForwardsServiceCalls()
    {
        var ownerPluginServices = CreateOwnerPluginServices();
        var ownerBuilder = new PluginServicesLocatorBuilder(ownerPluginServices, []);
        ownerBuilder.Build();

        var importingPluginServices = new PluginServiceCollection(new ServiceCollection(), []);
        var importingBuilder = new PluginServicesLocatorBuilder(importingPluginServices, [ownerPluginServices]);
        importingBuilder.Build();

        var importingProvider = importingPluginServices.ServiceProvider!;
        var importedService = importingProvider.GetRequiredService<ITestDisposablePublicService>();

        var callCount = importedService.Increment();

        Assert.Equal(1, callCount);

        ((IDisposable)importingProvider).Dispose();
        ((IDisposable)ownerPluginServices.ServiceProvider!).Dispose();
    }

    private static PluginServiceCollection CreateOwnerPluginServices()
    {
        var ownerServiceCollection = new ServiceCollection();
        ownerServiceCollection.AddSingleton<ITestDisposablePublicService, TestDisposablePublicService>();

        var publicDescriptor = ownerServiceCollection.Last(sd => sd.ServiceType == typeof(ITestDisposablePublicService));
        return new PluginServiceCollection(ownerServiceCollection, [publicDescriptor]);
    }

    private interface ITestDisposablePublicService : IDisposable
    {
        int DisposeCallCount { get; }

        int Increment();
    }

    private sealed class TestDisposablePublicService : ITestDisposablePublicService
    {
        private int _callCount;

        public int DisposeCallCount { get; private set; }

        public int Increment()
        {
            _callCount++;
            return _callCount;
        }

        public void Dispose()
        {
            DisposeCallCount++;
        }
    }
}
