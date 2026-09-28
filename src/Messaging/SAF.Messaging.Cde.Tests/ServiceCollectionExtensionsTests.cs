// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCde_BindsTheConfiguration_WhenNoCustomizerIsForwarded()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddCde(c => c.ApplicationName = "from configuration");

        var config = services.BuildServiceProvider().GetRequiredService<CdeConfiguration>();
        Assert.Equal("from configuration", config.ApplicationName);
    }

    [Fact]
    public void AddCde_AppliesForwardedCustomizers_AfterTheConfigurationIsBound()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // What the plugin system's forwarder puts into the plugin container.
        services.AddSingleton<IPluginOptionsCustomizer<CdeConfiguration>>(new SetsApplicationId("host-only-id"));

        services.AddCde(c =>
        {
            c.ApplicationName = "from configuration";
            c.ApplicationId = "configured-id";
        });

        var config = services.BuildServiceProvider().GetRequiredService<CdeConfiguration>();

        Assert.Equal("host-only-id", config.ApplicationId);
        Assert.Equal("from configuration", config.ApplicationName);
    }

    [Fact]
    public void AddCde_AppliesSeveralCustomizersInRegistrationOrder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPluginOptionsCustomizer<CdeConfiguration>>(new SetsApplicationId("first"));
        services.AddSingleton<IPluginOptionsCustomizer<CdeConfiguration>>(new SetsApplicationId("second"));

        services.AddCde(_ => { });

        var config = services.BuildServiceProvider().GetRequiredService<CdeConfiguration>();
        Assert.Equal("second", config.ApplicationId);
    }

    [Fact]
    public void ConfigureServices_LetsAForwardedCustomizerOverrideTheCdeSection()
    {
        var context = Substitute.For<IPluginSystemHostContext>();
        context.HostConfiguration.Returns(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cde:ApplicationId"] = "configured-id",
                ["Cde:ApplicationName"] = "configured-name"
            })
            .Build());
        context.PluginConfiguration.Returns(new ConfigurationBuilder().Build());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IPluginOptionsCustomizer<CdeConfiguration>>(new SetsApplicationId("host-only-id"));

        new PluginManifest().ConfigureServices(context, services);

        var config = services.BuildServiceProvider().GetRequiredService<CdeConfiguration>();

        Assert.Equal("host-only-id", config.ApplicationId);
        Assert.Equal("configured-name", config.ApplicationName);
    }

    private sealed class SetsApplicationId(string applicationId) : IPluginOptionsCustomizer<CdeConfiguration>
    {
        public void Customize(CdeConfiguration options) => options.ApplicationId = applicationId;
    }
}
