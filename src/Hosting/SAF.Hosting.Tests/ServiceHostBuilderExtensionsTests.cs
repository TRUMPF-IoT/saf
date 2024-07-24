// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Contracts;
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

    [Theory]
    [InlineData("", "pattern", "searchPath")]
    [InlineData("basePath", "", "searchPath")]
    [InlineData("basePath", "pattern", "")]
    public void ServiceAssemblySearchOptionsGetsValidatedOnServiceRequest(string basePath, string filenamePattern, string searchPath)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = new ServiceHostBuilder(services);

        // Act
        builder.AddServiceAssemblySearch(options =>
        {
            options.BasePath = basePath;
            options.SearchFilenamePattern = filenamePattern;
            options.SearchPath = searchPath;
        });
        var sp = builder.Services.BuildServiceProvider();

        // Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _ = sp.GetService<IServiceAssemblySearch>());

        if(string.IsNullOrEmpty(basePath))
            Assert.Contains("BasePath", ex.Message);
        if (string.IsNullOrEmpty(filenamePattern))
            Assert.Contains("SearchFilenamePattern", ex.Message);
        if (string.IsNullOrEmpty(searchPath))
            Assert.Contains("SearchPath", ex.Message);
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