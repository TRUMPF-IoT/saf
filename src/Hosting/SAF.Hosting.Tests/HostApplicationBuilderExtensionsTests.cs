// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.PluginSystem.Hosting;
using Xunit;

public class HostApplicationBuilderExtensionsTests
{
    [Fact]
    public void AddSafHost_WhenPluginContractsSearchPatternIsNotConfigured_AddsBuiltInSafContracts()
    {
        var services = new ServiceCollection();
        var builder = CreateBuilder(services, new ConfigurationManager());

        builder.AddSafHost();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<PluginSystemOptions>>().Value;

        Assert.Equal("SAF.Common.dll;SAF.Messaging.Contracts.dll", options.PluginContractsSearchPattern);
    }

    [Fact]
    public void AddSafHost_WhenPluginContractsSearchPatternIsConfigured_MergesBuiltInAndConfiguredContracts()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PluginSystem:PluginContractsSearchPattern"] = "Custom.Contracts.dll;SAF.Common.dll"
        });

        var services = new ServiceCollection();
        var builder = CreateBuilder(services, configuration);

        builder.AddSafHost();

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<PluginSystemOptions>>().Value;

        Assert.Equal("SAF.Common.dll;SAF.Messaging.Contracts.dll;Custom.Contracts.dll", options.PluginContractsSearchPattern);
    }

    [Fact]
    public void AddSafHost_WithExplicitPluginSystemConfiguration_MergesBuiltInAndConfiguredContracts()
    {
        var services = new ServiceCollection();
        var builder = CreateBuilder(services, new ConfigurationManager());

        builder.AddSafHost(options => options.PluginContractsSearchPattern = "Custom.Contracts.dll;SAF.Messaging.Contracts.dll");

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<PluginSystemOptions>>().Value;

        Assert.Equal("SAF.Common.dll;SAF.Messaging.Contracts.dll;Custom.Contracts.dll", options.PluginContractsSearchPattern);
    }

    private static IHostApplicationBuilder CreateBuilder(IServiceCollection services, ConfigurationManager configuration)
    {
        var builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);
        builder.Configuration.Returns(configuration);

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Test");
        builder.Environment.Returns(environment);

        return builder;
    }
}
