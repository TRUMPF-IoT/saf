// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginManifestTests
{
    [Fact]
    public void ConfigureServices_WithPluginConfiguration_RegistersNatsMessagingFactory()
    {
        var pluginConfig = new Dictionary<string, string?> { ["Nats:Servers"] = "nats://localhost:4222" };
        var hostConfig = new Dictionary<string, string?> { ["Nats:Servers"] = "nats://host:4222" };

        var context = CreateContext(hostConfig, pluginConfig);
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Nats key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Nats);
        Assert.NotNull(factory);
    }

    [Fact]
    public void ConfigureServices_WithoutPluginConfiguration_RegistersNatsMessagingFactory()
    {
        var hostConfig = new Dictionary<string, string?> { ["Nats:Servers"] = "nats://localhost:4222" };

        var context = CreateContext(hostConfig, new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Nats key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Nats);
        Assert.NotNull(factory);
    }

    [Fact]
    public void ConfigureServices_WithoutAnyConfiguration_RegistersNatsMessagingFactory()
    {
        var context = CreateContext(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the Nats key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.Nats);
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
