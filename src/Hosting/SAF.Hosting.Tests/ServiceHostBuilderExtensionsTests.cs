// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Abstractions;
using Hosting.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public class ServiceHostBuilderExtensionsTests
{
    [Fact]
    public void AddServiceAssemblySearchAddsRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ServiceHostBuilder(services);

        // Act
        builder.AddServiceAssemblySearch();

        // Assert
        Assert.NotEmpty(services);
        Assert.Contains(services,
            sd => sd.ServiceType == typeof(IServiceAssemblySearch) && sd.ImplementationType == typeof(ServiceAssemblySearch));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IConfigureOptions<ServiceAssemblySearchOptions>));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPostConfigureOptions<ServiceAssemblySearchOptions>));
    }

    [Fact]
    public void ServiceAssemblySearchOptionsGetsValidatedOnServiceRequest()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = new ServiceHostBuilder(services);

        // Act
        builder.AddServiceAssemblySearch(options => options.BasePath = string.Empty);
        var sp = builder.Services.BuildServiceProvider();

        // Assert
        Assert.Throws<InvalidOperationException>(() => _ = sp.GetService<IServiceAssemblySearch>());
    }

    [Fact]
    public void AddServiceAssemblyAddsServiceAssemblyManifest()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ServiceHostBuilder(services);

        // Act
        builder.AddServiceAssembly<DummyServiceAssemblyManifest>();

        // Assert
        Assert.NotEmpty(services);
        Assert.Contains(services, sd => sd.ServiceType == typeof(IServiceAssemblyManifest) && sd.ImplementationType == typeof(DummyServiceAssemblyManifest));
    }

    [Fact]
    public void AddHostDiagnosticsAddsService()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new ServiceHostBuilder(services);

        // Act
        builder.AddHostDiagnostics();

        // Assert
        Assert.NotEmpty(services);
        Assert.Contains(services, sd => sd.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) && sd.ImplementationType == typeof(ServiceHostDiagnostics));
    }

    private class DummyServiceAssemblyManifest : IServiceAssemblyManifest
    {
        public string FriendlyName => throw new NotImplementedException();
        public void RegisterDependencies(IServiceCollection services, IServiceHostContext context)
            => throw new System.NotImplementedException();
    }
}