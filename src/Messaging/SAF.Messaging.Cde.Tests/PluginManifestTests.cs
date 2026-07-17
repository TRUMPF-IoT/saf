// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Common;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginManifestTests
{
    [Fact]
    public void ConfigureServices_WithPluginConfiguration_RegistersCdeMessagingFactory()
    {
        var pluginConfig = new Dictionary<string, string?> { ["Cde:NodeName"] = "plugin-node" };
        var hostConfig = new Dictionary<string, string?> { ["Cde:NodeName"] = "host-node" };

        var context = CreateContext(hostConfig, pluginConfig);
        var services = new ServiceCollection();
        services.AddLogging();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Cde key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Cde);
        Assert.NotNull(factory);

        // Verify IStorageInfrastructure is registered
        var storageDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IStorageInfrastructure));
        Assert.NotNull(storageDescriptor);
    }

    [Fact]
    public void ConfigureServices_WithoutPluginConfiguration_RegistersCdeMessagingFactory()
    {
        var hostConfig = new Dictionary<string, string?> { ["Cde:NodeName"] = "host-node" };

        var context = CreateContext(hostConfig, new Dictionary<string, string?>());
        var services = new ServiceCollection();
        services.AddLogging();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Cde key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Cde);
        Assert.NotNull(factory);

        // Verify IStorageInfrastructure is registered
        var storageDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IStorageInfrastructure));
        Assert.NotNull(storageDescriptor);
    }

    [Fact]
    public void ConfigureServices_WithoutAnyConfiguration_RegistersCdeMessagingFactory()
    {
        var context = CreateContext(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var services = new ServiceCollection();
        services.AddLogging();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Cde key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Cde);
        Assert.NotNull(factory);

        // Verify IStorageInfrastructure is registered
        var storageDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IStorageInfrastructure));
        Assert.NotNull(storageDescriptor);
    }

    [Fact]
    public void ConfigureServices_WithDiagnosticsEnabled_RegistersCdeServices()
    {
        var pluginConfig = new Dictionary<string, string?>
        {
            ["Cde:NodeName"] = "diag-node",
            ["Cde:EnableDiagnostics"] = "true"
        };
        var hostConfig = new Dictionary<string, string?>();

        var context = CreateContext(hostConfig, pluginConfig);
        var services = new ServiceCollection();
        services.AddLogging();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Cde key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Cde);
        Assert.NotNull(factory);
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
