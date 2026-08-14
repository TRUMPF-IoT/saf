// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Reflection;
using System.IO.Abstractions;
using Testably.Abstractions.Testing;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

public class PluginSystemHostBuilderExtensionsTests
{
    private readonly IPluginSystemHostBuilder _hostBuilder = Substitute.For<IPluginSystemHostBuilder>();
    private readonly ServiceCollection _serviceCollection = [];

    public PluginSystemHostBuilderExtensionsTests()
    {
        _serviceCollection.AddTransient(typeof(ILogger<>), typeof(Logger<>));
        _serviceCollection.AddTransient(typeof(ILoggerFactory), _ => Substitute.For<ILoggerFactory>());
        _serviceCollection.AddSingleton<IFileSystem>(new MockFileSystem());
        _serviceCollection.AddSingleton<IPluginManifestLoader, PluginManifestLoader>();

        _hostBuilder.Services.Returns(_serviceCollection);
    }

    [Fact]
    public void AddPluginAssemblyFolderContainer_ThrowsIfHostBuilderEqualsNull()
    {
        IPluginSystemHostBuilder? hostBuilder = null;
        Assert.Throws<ArgumentNullException>(() => hostBuilder!.AddPluginAssemblyFolderContainer());
    }

    [Fact]
    public void AddPluginAssemblyFolderContainer_WithConfiguration_ThrowsIfHostBuilderEqualsNull()
    {
        IPluginSystemHostBuilder? hostBuilder = null;
        Assert.Throws<ArgumentNullException>(() => hostBuilder!.AddPluginAssemblyFolderContainer(_ => { }));
    }

    [Fact]
    public void AddPluginAssemblyFolderContainer_ShouldAddRequiredServices()
    {
        // Act
        _hostBuilder.AddPluginAssemblyFolderContainer();

        // Assert
        var serviceProvider = _serviceCollection.BuildServiceProvider();
        Assert.NotNull(serviceProvider.GetService<IPluginAssemblyContainer>() as PluginAssemblyFolderContainer);
        Assert.Empty(serviceProvider.GetServices<IPluginAssemblyValidator>());
    }

    [Fact]
    public void AddPluginAssemblyFolderContainer_WithConfiguration_ShouldConfigureOptions()
    {
        // Arrange
        const string configuredPath = "configured_path";

        // Act
        _hostBuilder.AddPluginAssemblyFolderContainer(options => options.SearchRootPath = configuredPath);

        // Assert
        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var service = serviceProvider.GetService<IPluginAssemblyContainer>() as PluginAssemblyFolderContainer;
        Assert.NotNull(service);

        var searchOptions = service.GetType().GetProperty("SearchOptions", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(service) as PluginAssemblyFolderSearchOptions;
        Assert.Equal(configuredPath, searchOptions!.SearchRootPath);
    }

    [Fact]
    public void AddPluginConfigurationSource_ThrowsIfHostBuilderEqualsNull()
    {
        IPluginSystemHostBuilder? hostBuilder = null;
        Assert.Throws<ArgumentNullException>(() => hostBuilder!.AddPluginConfigurationSource(_ => { }));
    }

    [Fact]
    public void AddPluginConfigurationSource_ThrowsIfConfigureSourceEqualsNull()
        => Assert.Throws<ArgumentNullException>(() => _hostBuilder.AddPluginConfigurationSource(null!));

    [Fact]
    public void AddPluginConfigurationSource_MultipleCalls_PreserveRegistrationOrder()
    {
        // Arrange
        _hostBuilder.AddPluginConfigurationSource(sourceContext =>
            sourceContext.Builder.AddInMemoryCollection(new Dictionary<string, string?> { ["Key"] = "First" }));
        _hostBuilder.AddPluginConfigurationSource(sourceContext =>
            sourceContext.Builder.AddInMemoryCollection(new Dictionary<string, string?> { ["Key"] = "Second" }));

        // Act
        var serviceProvider = _serviceCollection.BuildServiceProvider();
        var configureSources = serviceProvider.GetRequiredService<IOptions<PluginConfigurationSourcesOptions>>().Value.ConfigureSources;

        var configurationBuilder = new ConfigurationBuilder();
        var sourceContext = new PluginConfigurationSourceContext
        {
            Builder = configurationBuilder,
            SettingsFileProvider = null,
            EnvironmentName = "Test",
            SettingsFileName = null,
            OnLoadException = _ => { },
            HostServices = Substitute.For<IServiceProvider>(),
        };
        foreach (var configureSource in configureSources)
            configureSource(sourceContext);
        var configuration = configurationBuilder.Build();

        // Assert — registration order is preserved, so the second call's provider overrides the first's.
        Assert.Equal(2, configureSources.Count);
        Assert.Equal("Second", configuration["Key"]);
    }
}