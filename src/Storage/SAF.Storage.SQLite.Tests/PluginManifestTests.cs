// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.SQLite.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Common;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginManifestTests
{
    [Fact]
    public void ConfigureServices_WithPluginSQLiteConfiguration_RegistersStorageInfrastructure()
    {
        var pluginConfig = new Dictionary<string, string?> { ["SQLite:ConnectionString"] = "Data Source=plugin.db" };
        var hostConfig = new Dictionary<string, string?> { ["SQLite:ConnectionString"] = "Data Source=host.db" };

        var context = CreateContext(hostConfig, pluginConfig);
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IStorageInfrastructure is registered
        var storage = provider.GetService<IStorageInfrastructure>();
        Assert.NotNull(storage);
        Assert.IsType<Storage>(storage);
    }

    [Fact]
    public void ConfigureServices_WithLegacyPluginSQLiteConfiguration_RegistersStorageInfrastructure()
    {
        var pluginConfig = new Dictionary<string, string?> { ["SQLiteConfiguration:ConnectionString"] = "Data Source=legacy.db" };
        var hostConfig = new Dictionary<string, string?>();

        var context = CreateContext(hostConfig, pluginConfig);
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IStorageInfrastructure is registered
        var storage = provider.GetService<IStorageInfrastructure>();
        Assert.NotNull(storage);
        Assert.IsType<Storage>(storage);
    }

    [Fact]
    public void ConfigureServices_WithoutPluginConfiguration_RegistersStorageInfrastructure()
    {
        var hostConfig = new Dictionary<string, string?> { ["SQLite:ConnectionString"] = "Data Source=host.db" };

        var context = CreateContext(hostConfig, new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IStorageInfrastructure is registered
        var storage = provider.GetService<IStorageInfrastructure>();
        Assert.NotNull(storage);
        Assert.IsType<Storage>(storage);
    }

    [Fact]
    public void ConfigureServices_WithoutAnyConfiguration_ThrowsArgumentException()
    {
        var context = CreateContext(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Attempting to resolve IStorageInfrastructure without valid connection string should throw
        Assert.Throws<ArgumentException>(() => provider.GetService<IStorageInfrastructure>());
    }

    private static IPluginSystemHostContext CreateContext(
        IReadOnlyDictionary<string, string?> hostValues,
        IReadOnlyDictionary<string, string?> pluginValues)
    {
        var context = Substitute.For<IPluginSystemHostContext>();
        context.HostConfiguration.Returns(new ConfigurationBuilder().AddInMemoryCollection(hostValues).Build());
        context.PluginConfiguration.Returns(new ConfigurationBuilder().AddInMemoryCollection(pluginValues).Build());
        return context;
    }
}
