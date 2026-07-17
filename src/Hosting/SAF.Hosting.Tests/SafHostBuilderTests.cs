// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class SafHostBuilderTests
{
    [Fact]
    public void ConfigureHostInfo_WithValidAction_RegistersConfigurationInServices()
    {
        var services = new ServiceCollection();
        var pluginSystemHostBuilder = Substitute.For<IPluginSystemHostBuilder>();
        pluginSystemHostBuilder.Services.Returns(services);
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);

        safHostBuilder.ConfigureHostInfo(options => options.ServiceHostType = "Custom");

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ServiceHostOptions>>().Value;

        Assert.Equal("Custom", options.ServiceHostType);
    }

    [Fact]
    public void ConfigureHostInfo_WithNullAction_ThrowsArgumentNullException()
    {
        var pluginSystemHostBuilder = CreateMockPluginSystemHostBuilder();
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            safHostBuilder.ConfigureHostInfo(null!));

        Assert.Equal("configure", exception.ParamName);
    }

    [Fact]
    public void ConfigureHostInfo_ReturnsBuilderForChaining()
    {
        var pluginSystemHostBuilder = CreateMockPluginSystemHostBuilder();
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);

        var result = safHostBuilder.ConfigureHostInfo(_ => { });

        Assert.Same(safHostBuilder, result);
    }

    [Fact]
    public void ConfigureHostInfo_AllowsMultipleConfigurationsToChain()
    {
        var services = new ServiceCollection();
        var pluginSystemHostBuilder = Substitute.For<IPluginSystemHostBuilder>();
        pluginSystemHostBuilder.Services.Returns(services);
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);

        var result = safHostBuilder
            .ConfigureHostInfo(o => o.ServiceHostType = "First")
            .ConfigureHostInfo(o => o.Id = "test-id");

        Assert.Same(safHostBuilder, result);

        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ServiceHostOptions>>().Value;

        Assert.Equal("First", options.ServiceHostType);
        Assert.Equal("test-id", options.Id);
    }

    [Fact]
    public void ConfigurePluginSystem_WithValidAction_CallsActionWithPluginSystemBuilder()
    {
        var pluginSystemHostBuilder = CreateMockPluginSystemHostBuilder();
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);
        IPluginSystemHostBuilder? capturedBuilder = null;

        void ConfigureAction(IPluginSystemHostBuilder builder)
        {
            capturedBuilder = builder;
        }

        safHostBuilder.ConfigurePluginSystem(ConfigureAction);

        Assert.Same(pluginSystemHostBuilder, capturedBuilder);
    }

    [Fact]
    public void ConfigurePluginSystem_WithNullAction_ThrowsArgumentNullException()
    {
        var pluginSystemHostBuilder = CreateMockPluginSystemHostBuilder();
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            safHostBuilder.ConfigurePluginSystem(null!));

        Assert.Equal("configure", exception.ParamName);
    }

    [Fact]
    public void ConfigurePluginSystem_ReturnsBuilderForChaining()
    {
        var pluginSystemHostBuilder = CreateMockPluginSystemHostBuilder();
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);

        var result = safHostBuilder.ConfigurePluginSystem(_ => { });

        Assert.Same(safHostBuilder, result);
    }

    [Fact]
    public void ConfigurePluginSystem_AllowsMultipleCalls()
    {
        var pluginSystemHostBuilder = CreateMockPluginSystemHostBuilder();
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);
        var callCount = 0;

        var result = safHostBuilder
            .ConfigurePluginSystem(_ => callCount++)
            .ConfigurePluginSystem(_ => callCount++);

        Assert.Equal(2, callCount);
        Assert.Same(safHostBuilder, result);
    }

    [Fact]
    public void AddHostDiagnostics_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging(); // Required for ServiceHostDiagnostics
        var pluginSystemHostBuilder = Substitute.For<IPluginSystemHostBuilder>();
        pluginSystemHostBuilder.Services.Returns(services);
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);

        safHostBuilder.AddHostDiagnostics();

        // Verify that ServiceHostDiagnostics was registered as a hosted service
        var serviceDescriptors = services.Where(s => s.ServiceType == typeof(IHostedService)).ToList();
        Assert.NotEmpty(serviceDescriptors);
    }

    [Fact]
    public void AddHostDiagnostics_ReturnsBuilderForChaining()
    {
        var pluginSystemHostBuilder = CreateMockPluginSystemHostBuilder();
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);

        var result = safHostBuilder.AddHostDiagnostics();

        Assert.Same(safHostBuilder, result);
    }

    [Fact]
    public void FluentBuilder_AllMethodsChainable()
    {
        var pluginSystemHostBuilder = CreateMockPluginSystemHostBuilder();
        var safHostBuilder = new SafHostBuilder(pluginSystemHostBuilder);

        var result = safHostBuilder
            .ConfigureHostInfo(o => o.ServiceHostType = "Test")
            .ConfigurePluginSystem(_ => { })
            .AddHostDiagnostics()
            .ConfigureHostInfo(o => o.Id = "builder-test")
            .ConfigurePluginSystem(_ => { });

        Assert.Same(safHostBuilder, result);
    }

    private static IPluginSystemHostBuilder CreateMockPluginSystemHostBuilder()
    {
        var services = new ServiceCollection();
        var pluginSystemHostBuilder = Substitute.For<IPluginSystemHostBuilder>();
        pluginSystemHostBuilder.Services.Returns(services);
        return pluginSystemHostBuilder;
    }
}
