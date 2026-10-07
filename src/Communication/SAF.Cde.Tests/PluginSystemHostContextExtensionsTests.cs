// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde.Tests;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginSystemHostContextExtensionsTests
{
    [Fact]
    public void GetCdeConfigurationSection_ReturnsThePluginSection_WhenThePluginConfigurationHasOne()
    {
        var context = CreateContext(
            host: new Dictionary<string, string?> { ["Cde:ScopeId"] = "host" },
            plugin: new Dictionary<string, string?> { ["Cde:ScopeId"] = "plugin" });

        var section = context.GetCdeConfigurationSection();

        Assert.Equal("plugin", section["ScopeId"]);
    }

    [Fact]
    public void GetCdeConfigurationSection_FallsBackToTheHostSection_WhenThePluginConfigurationHasNone()
    {
        var context = CreateContext(
            host: new Dictionary<string, string?> { ["Cde:ScopeId"] = "host" },
            plugin: new Dictionary<string, string?>());

        var section = context.GetCdeConfigurationSection();

        Assert.Equal("host", section["ScopeId"]);
    }

    private static IPluginSystemHostContext CreateContext(
        IReadOnlyDictionary<string, string?> host,
        IReadOnlyDictionary<string, string?> plugin)
    {
        var context = Substitute.For<IPluginSystemHostContext>();
        context.HostConfiguration.Returns(new ConfigurationBuilder().AddInMemoryCollection(host).Build());
        context.PluginConfiguration.Returns(new ConfigurationBuilder().AddInMemoryCollection(plugin).Build());
        return context;
    }
}