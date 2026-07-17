// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Linq;
using TestPlugin.PublicDependencyA;
using Testably.Abstractions;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginServicesIsolationTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginServicesContainer> _logger;
    private readonly IPluginSystemHostContext _hostContext;
    private readonly IPluginAssemblyContainer _pluginContainer;
    private readonly IServiceProvider _applicationServiceProvider;
    private readonly IPublicServiceTypeRegistry _publicServiceTypeRegistry;

    public PluginServicesIsolationTests(ITestOutputHelper outputHelper)
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Warning));
        _logger = _loggerFactory.CreateLogger<PluginServicesContainer>();

        _hostContext = Substitute.For<IPluginSystemHostContext>();
        _applicationServiceProvider = Substitute.For<IServiceProvider>();
        _publicServiceTypeRegistry = Substitute.For<IPublicServiceTypeRegistry>();
        _publicServiceTypeRegistry.GetAssemblyNames().Returns(new[] { typeof(IPublicSingleton).Assembly.FullName! });

        _pluginContainer = new PluginAssemblyFolderContainer(
            _loggerFactory,
            new PluginManifestLoader(),
            new PluginAssemblyFolderSearchOptions
            {
                SearchRootPath = Path.Combine(AppContext.BaseDirectory, "plugins"),
                IncludePatterns = "TestPlugin.Plugin*.dll",
                Recursive = true
            },
            [],
            new RealFileSystem());
    }

    [Fact]
    public void GetPluginServices_ShouldReturnIsolatedServiceProviders()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var pluginServiceProviders = pluginServicesContainer.GetPluginServices().ToList();

        // Assert
        Assert.Equal(2, pluginServiceProviders.Count);
        Assert.All(pluginServiceProviders, sp => Assert.NotSame(pluginServicesContainer.GetPublicServices(), sp));
    }

    [Fact]
    public void GetPublicServices_ShouldReturnPublicServicesServiceProvider()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var publicServiceProvider = pluginServicesContainer.GetPublicServices();

        // Assert
        Assert.NotNull(publicServiceProvider);
        Assert.All(pluginServicesContainer.GetPluginServices(), sp => Assert.NotSame(publicServiceProvider, sp));
    }

    [Fact]
    public void IsolatedPluginServices_ShouldContainPublicServices()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var pluginServiceProviders = pluginServicesContainer.GetPluginServices();

        // Assert
        foreach (var sp in pluginServiceProviders)
        {
            Assert.NotNull(sp.GetService(typeof(IPublicSingleton)));
            Assert.NotEmpty(sp.GetKeyedServices<IPublicSingleton>("ssA"));
            Assert.NotEmpty(sp.GetKeyedServices<IPublicSingleton>("ssB"));
            Assert.NotNull(sp.GetService(typeof(IPublicTransient)));
            Assert.NotEmpty(sp.GetKeyedServices<IPublicTransient>("ssA"));
            Assert.NotEmpty(sp.GetKeyedServices<IPublicTransient>("ssB"));
        }
    }

    [Fact]
    public void PublicServices_ShouldContainAllPublicServices()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var pluginServiceProvider = pluginServicesContainer.GetPublicServices();

        // Assert
        Assert.NotNull(pluginServiceProvider.GetService<IPublicSingleton>());
        Assert.Equal(2, pluginServiceProvider.GetServices<IPublicSingleton>().Count());
        Assert.NotNull(pluginServiceProvider.GetService<IPublicTransient>());
        Assert.Equal(2, pluginServiceProvider.GetServices<IPublicTransient>().Count());
    }

    [Fact]
    public void IsolatedPluginServices_ShouldAccessPrivateServices()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var pluginServiceProviders = pluginServicesContainer.GetPluginServices();

        // Assert
        foreach (var sp in pluginServiceProviders)
        {
            var publicSingleton = sp.GetRequiredService<IPublicSingleton>();
            Assert.NotNull(publicSingleton.GetPrivateSingleton());
            Assert.NotNull(publicSingleton.GetPrivateTransient());
        }
    }

    [Fact]
    public void PublicServices_ShouldNotContainPrivateServices()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var pluginServiceProvider = pluginServicesContainer.GetPublicServices();

        // Assert
        foreach(var singleton in pluginServiceProvider.GetServices<IPublicSingleton>())
        {
            Assert.Null(pluginServiceProvider.GetService(singleton.GetPrivateSingletonType()));
            Assert.Null(pluginServiceProvider.GetService(singleton.GetPrivateTransientType()));
        }
    }

    [Fact]
    public void IsolatedPluginServices_ShouldNotAccessPrivateServicesOfOtherPlugins()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var pluginServiceProviders = pluginServicesContainer.GetPluginServices();

        // Assert
        foreach (var sp in pluginServiceProviders)
        {
            var publicTransient = sp.GetRequiredService<IPublicTransient>();
            Assert.Null(publicTransient.GetPrivateServiceOfOtherPlugin());
        }
    }

    [Fact]
    public void PublicSingletonService_IsSingletonBetweenAllContainers()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var pluginServiceProviders = pluginServicesContainer.GetPluginServices();
        var publicServiceProvider = pluginServicesContainer.GetPublicServices();

        // Assert
        List<IPublicSingleton> firstPublicSingletons = new();
        foreach (var sp in pluginServiceProviders)
        {
            var publicSingletons = sp.GetServices<IPublicSingleton>().ToList();
            Assert.Equal(2, publicSingletons.Count);
            Assert.True(publicSingletons.SequenceEqual(sp.GetServices<IPublicSingleton>()));

            if(firstPublicSingletons.Count == 0)
            {
                firstPublicSingletons.AddRange(publicSingletons);
            }
            else
            {
                Assert.All(publicSingletons, singleton => Assert.Contains(singleton, firstPublicSingletons));
            }
        }

        var publicSpSingletons = publicServiceProvider.GetServices<IPublicSingleton>().ToList();
        Assert.Equal(2, publicSpSingletons.Count);
        Assert.True(publicSpSingletons.SequenceEqual(publicServiceProvider.GetServices<IPublicSingleton>()));
        Assert.All(publicSpSingletons, singleton => Assert.Contains(singleton, firstPublicSingletons));
    }

    [Fact]
    public void PublicTransientService_IsTransientBetweenIsolatedContainers()
    {
        // Arrange
        var pluginServicesContainer = new PluginServicesContainer(_logger, _hostContext, _applicationServiceProvider, [_pluginContainer], _publicServiceTypeRegistry);

        // Act
        var pluginServiceProviders = pluginServicesContainer.GetPluginServices();
        var publicServiceProvider = pluginServicesContainer.GetPublicServices();

        // Assert
        List<IPublicTransient> firstPublicTransients = new();
        foreach (var sp in pluginServiceProviders)
        {
            var publicTransients = sp.GetServices<IPublicTransient>().ToList();
            Assert.Equal(2, publicTransients.Count);
            Assert.All(publicTransients, transient => Assert.DoesNotContain(transient, sp.GetServices<IPublicTransient>()));

            if (firstPublicTransients.Count == 0)
            {
                firstPublicTransients.AddRange(publicTransients);
            }
            else
            {
                Assert.All(publicTransients, transient => Assert.DoesNotContain(transient, firstPublicTransients));
            }
        }

        var publicSpTransients = publicServiceProvider.GetServices<IPublicTransient>().ToList();
        Assert.Equal(2, publicSpTransients.Count);
        Assert.All(publicSpTransients, transient => Assert.DoesNotContain(transient, publicServiceProvider.GetServices<IPublicTransient>()));
        Assert.All(publicSpTransients, transient => Assert.DoesNotContain(transient, firstPublicTransients));
    }
}
