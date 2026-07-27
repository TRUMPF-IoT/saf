// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Common;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginManifestTests
{
    [Fact]
    public void ConfigureServices_WithPluginConfiguration_RegistersRedisMessagingFactory()
    {
        var pluginConfig = new Dictionary<string, string?> { ["Redis:ConnectionString"] = "localhost:6379" };
        var hostConfig = new Dictionary<string, string?> { ["Redis:ConnectionString"] = "host:6379" };

        var context = CreateContext(hostConfig, pluginConfig);
        var services = new ServiceCollection();
        services.AddLogging();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Redis key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Redis);
        Assert.NotNull(factory);

        // Verify IStorageInfrastructure is registered (don't instantiate it as it requires Redis connection)
        var storageDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IStorageInfrastructure));
        Assert.NotNull(storageDescriptor);
    }

    [Fact]
    public void ConfigureServices_WithoutPluginConfiguration_RegistersRedisMessagingFactory()
    {
        var hostConfig = new Dictionary<string, string?> { ["Redis:ConnectionString"] = "localhost:6379" };

        var context = CreateContext(hostConfig, new Dictionary<string, string?>());
        var services = new ServiceCollection();
        services.AddLogging();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Redis key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Redis);
        Assert.NotNull(factory);

        // Verify IStorageInfrastructure is registered
        var storageDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IStorageInfrastructure));
        Assert.NotNull(storageDescriptor);
    }

    [Fact]
    public void ConfigureServices_WithoutAnyConfiguration_RegistersRedisMessagingFactory()
    {
        var context = CreateContext(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var services = new ServiceCollection();
        services.AddLogging();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Redis key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Redis);
        Assert.NotNull(factory);

        // Verify IStorageInfrastructure is registered
        var storageDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IStorageInfrastructure));
        Assert.NotNull(storageDescriptor);
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
