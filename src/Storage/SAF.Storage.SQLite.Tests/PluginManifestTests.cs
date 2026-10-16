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
        var pluginConfig = new Dictionary<string, string?> { ["SQLite:ConnectionString"] = "Data Source=:memory:;Version=3;" };
        var hostConfig = new Dictionary<string, string?> { ["SQLite:ConnectionString"] = "Data Source=host.db" };

        var context = CreateContext(hostConfig, pluginConfig);
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        using var provider = services.BuildServiceProvider();

        // Verify IStorageInfrastructure is registered
        var storage = provider.GetService<IStorageInfrastructure>();
        Assert.NotNull(storage);
        Assert.IsType<Storage>(storage);
    }

    [Fact]
    public void ConfigureServices_WithLegacyPluginSQLiteConfiguration_RegistersStorageInfrastructure()
    {
        var pluginConfig = new Dictionary<string, string?> { ["SQLiteConfiguration:ConnectionString"] = "Data Source=:memory:;Version=3;" };
        var hostConfig = new Dictionary<string, string?>();

        var context = CreateContext(hostConfig, pluginConfig);
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        using var provider = services.BuildServiceProvider();

        // Verify IStorageInfrastructure is registered
        var storage = provider.GetService<IStorageInfrastructure>();
        Assert.NotNull(storage);
        Assert.IsType<Storage>(storage);
    }

    [Fact]
    public void ConfigureServices_WithoutPluginConfiguration_RegistersStorageInfrastructure()
    {
        var hostConfig = new Dictionary<string, string?> { ["SQLite:ConnectionString"] = "Data Source=:memory:;Version=3;" };

        var context = CreateContext(hostConfig, new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        using var provider = services.BuildServiceProvider();

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

        using var provider = services.BuildServiceProvider();

        // Attempting to resolve IStorageInfrastructure without valid connection string should throw
        Assert.Throws<ArgumentException>(() => provider.GetService<IStorageInfrastructure>());
    }

    [Theory]
    [InlineData("SQLite", true)]
    [InlineData("SQLite", false)]
    [InlineData("SQLiteConfiguration", true)]
    [InlineData("SQLiteConfiguration", false)]
    public void ConfigureServices_WithLegacyVersionOption_StorageIsUsable(string section, bool pluginConfiguration)
    {
        var values = new Dictionary<string, string?> { [$"{section}:ConnectionString"] = "Data Source=:memory:;Version=3;" };
        var empty = new Dictionary<string, string?>();
        var context = pluginConfiguration ? CreateContext(empty, values) : CreateContext(values, empty);
        var services = new ServiceCollection();
        new PluginManifest().ConfigureServices(context, services);
        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IStorageInfrastructure>();

        storage.Set("text", "value");
        storage.Set("area", "bytes", new byte[] { 0, 1, 255 });
        Assert.Equal("value", storage.GetString("text"));
        Assert.Equal(new byte[] { 0, 1, 255 }, storage.GetBytes("area", "bytes"));
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
