// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using SAF.PluginSystem.Hosting.AssemblyLoading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        _serviceCollection.AddSingleton<ISharedAssemblyResolver>(Substitute.For<ISharedAssemblyResolver>());
        _serviceCollection.AddOptions();

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
}