// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Routing.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Routing;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginManifestTests
{
    [Fact]
    public void ConfigureServices_WithPluginConfiguration_RegistersRoutingMessagingServices()
    {
        var pluginConfig = new Dictionary<string, string?> { ["MessageRouting:Enabled"] = "true" };
        var hostConfig = new Dictionary<string, string?> { ["MessageRouting:Enabled"] = "false" };

        var context = CreateContext(hostConfig, pluginConfig);
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Routing key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Routing);
        Assert.NotNull(factory);

        // Verify IRoutingMessagingInfrastructure is registered
        var routing = provider.GetService<IRoutingMessagingInfrastructure>();
        Assert.NotNull(routing);
    }

    [Fact]
    public void ConfigureServices_WithoutPluginConfiguration_RegistersRoutingMessagingServices()
    {
        var hostConfig = new Dictionary<string, string?> { ["MessageRouting:Enabled"] = "true" };

        var context = CreateContext(hostConfig, new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Routing key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Routing);
        Assert.NotNull(factory);
    }

    [Fact]
    public void ConfigureServices_WithoutAnyConfiguration_RegistersRoutingMessagingServices()
    {
        var context = CreateContext(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Routing key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Routing);
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
