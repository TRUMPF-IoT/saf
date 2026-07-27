// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.InProcess.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginManifestTests
{
    [Fact]
    public void ConfigureServices_RegistersInProcessMessagingServices()
    {
        var context = CreateContext(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();

        // Verify IMessagingInfrastructureFactory is registered with the InProcess key
        var factory = provider.GetKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.InProcess);
        Assert.NotNull(factory);
        Assert.IsType<DelegatingMessagingInfrastructureFactory>(factory);
    }

    [Fact]
    public void ConfigureServices_CanBuildServiceProvider()
    {
        var context = CreateContext(new Dictionary<string, string?>(), new Dictionary<string, string?>());
        var services = new ServiceCollection();

        var manifest = new PluginManifest();
        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider);
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
