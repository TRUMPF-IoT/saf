// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Runtime.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginManifestTests
{
    [Fact]
    public void ConfigureServices_WhenPrimaryKeyMissing_ThrowsInvalidOperationException()
    {
        var context = CreateContext(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var manifest = new PluginManifest();

        Assert.Throws<InvalidOperationException>(() => manifest.ConfigureServices(context, services));
    }

    [Fact]
    public void ConfigureServices_WhenPrimaryKeyConfigured_ResolvesInfrastructureFromMatchingFactory()
    {
        const string pluginPrimaryKey = "PluginPrimary";
        const string hostPrimaryKey = "HostPrimary";

        var infrastructure = Substitute.For<IMessagingInfrastructure>();
        var factory = Substitute.For<IMessagingInfrastructureFactory>();
        factory.Create(Arg.Any<MessagingConfiguration>()).Returns(infrastructure);

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IMessagingInfrastructureFactory>(pluginPrimaryKey, factory);

        var context = CreateContext(
            new Dictionary<string, string?> { ["Messaging:PrimaryKey"] = hostPrimaryKey },
            new Dictionary<string, string?> { ["Messaging:PrimaryKey"] = pluginPrimaryKey });

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();
        var resolvedInfrastructure = provider.GetRequiredService<IMessagingInfrastructure>();

        Assert.Same(infrastructure, resolvedInfrastructure);
        factory.Received(1).Create(Arg.Is<MessagingConfiguration>(configuration => configuration.Key == pluginPrimaryKey));
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
