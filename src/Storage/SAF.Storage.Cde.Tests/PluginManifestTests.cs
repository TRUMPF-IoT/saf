// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.Cde.Tests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.Cde.Common;
using SAF.Common;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

// These tests run outside the CdeCollection, so they resolve only the configuration: resolving the lease would
// start C-DEngine without the lease of the CdeFixture, and disposing the container would then shut it down for
// the rest of the test process. SharedCdeNodeTests resolve the plug-in on the node.
public class PluginManifestTests
{
    [Fact]
    public void ConfigureServices_RegistersTheStorageOnTheCdeNode()
    {
        var services = ConfigureServices(new Dictionary<string, string?>(), new Dictionary<string, string?>());

        var storage = Assert.Single(services, d => d.ServiceType == typeof(IStorageInfrastructure));
        Assert.Equal(ServiceLifetime.Singleton, storage.Lifetime);
        Assert.Single(services, d => d.ServiceType == typeof(CdeNodeLease));
    }

    [Fact]
    public void ConfigureServices_WithPluginConfiguration_BindsThePluginCdeSection()
    {
        var pluginConfig = new Dictionary<string, string?> { ["Cde:ScopeId"] = "plugin-scope" };
        var hostConfig = new Dictionary<string, string?> { ["Cde:ScopeId"] = "host-scope" };

        var services = ConfigureServices(hostConfig, pluginConfig);

        using var provider = services.BuildServiceProvider();
        Assert.Equal("plugin-scope", provider.GetRequiredService<CdeConfiguration>().ScopeId);
    }

    [Fact]
    public void ConfigureServices_WithoutPluginConfiguration_BindsTheHostCdeSection()
    {
        var hostConfig = new Dictionary<string, string?> { ["Cde:ScopeId"] = "host-scope" };

        var services = ConfigureServices(hostConfig, new Dictionary<string, string?>());

        using var provider = services.BuildServiceProvider();
        Assert.Equal("host-scope", provider.GetRequiredService<CdeConfiguration>().ScopeId);
    }

    private static ServiceCollection ConfigureServices(
        IReadOnlyDictionary<string, string?> hostValues,
        IReadOnlyDictionary<string, string?> pluginValues)
    {
        var context = Substitute.For<IPluginSystemHostContext>();
        context.HostConfiguration.Returns(new ConfigurationBuilder().AddInMemoryCollection(hostValues).Build());
        context.PluginConfiguration.Returns(new ConfigurationBuilder().AddInMemoryCollection(pluginValues).Build());

        var services = new ServiceCollection();
        new PluginManifest().ConfigureServices(context, services);
        return services;
    }
}