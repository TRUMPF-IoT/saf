// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting.Tests;

using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

public class HostApplicationBuilderExtensionsTests
{
    [Fact]
    public void AddPluginSystem_ShouldReturnPluginSystemHostBuilder()
    {
        var services = new ServiceCollection();

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Test");
        var configManager = Substitute.For<IConfigurationManager>();
        var builder = Substitute.For<IHostApplicationBuilder>();
        builder.Configuration.Returns(configManager);
        builder.Environment.Returns(environment);
        builder.Services.Returns(services);

        var pluginSystemHostBuilder = builder.AddPluginSystem(_ => { });

        Assert.Equal(services, pluginSystemHostBuilder.Services);
        Assert.Equal("Test", pluginSystemHostBuilder.Environment.EnvironmentName);
        Assert.Equal(configManager, pluginSystemHostBuilder.Configuration);
    }

    [Fact]
    public void AddPluginSystem_ShouldAddRequiredServices()
    {
        var services = new ServiceCollection();

        var builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);

        builder.AddPluginSystem(_ => { });

        Assert.Contains(services, s => s.ServiceType == typeof(IConfigureOptions<PluginSystemOptions>));
        Assert.Contains(services, s => s.ServiceType == typeof(IPluginSystemHostEnvironment));
        Assert.Contains(services, s => s.ServiceType == typeof(IPluginSystemHostContext));
        Assert.Contains(services, s => s.ServiceType == typeof(IPluginServicesContainer));
        Assert.Contains(services, s => s.ServiceType == typeof(IPluginServiceProvider));
        Assert.Contains(services, s => s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(ServicePluginHost));
    }

    [Fact]
    public void AddPluginSystem_ConfiguresPluginSystemHostContext()
    {
        var services = new ServiceCollection();

        var builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);

        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Is(nameof(PluginSystemHostContext)))
            .Returns(NullLogger<PluginSystemHostContext>.Instance);
        services.AddSingleton(loggerFactory);
        services.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        builder.AddPluginSystem(_ => { });

        var serviceProvider = services.BuildServiceProvider();
        var hostContext = serviceProvider.GetRequiredService<IPluginSystemHostContext>();

        Assert.NotNull(hostContext);
    }

    [Fact]
    public void AddPluginSystem_ConfiguresPluginSystemHostEnvironment()
    {
        var services = new ServiceCollection();

        var builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);

        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Is(nameof(PluginSystemHostContext)))
            .Returns(NullLogger<PluginSystemHostContext>.Instance);
        services.AddSingleton(loggerFactory);
        services.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        builder.AddPluginSystem(sp => { sp.PluginSettingsRootPath = "./test-plugin-configs"; });

        var serviceProvider = services.BuildServiceProvider();
        var hostContext = serviceProvider.GetRequiredService<IPluginSystemHostContext>();
        var environment = serviceProvider.GetRequiredService<IPluginSystemHostEnvironment>();

        Assert.NotNull(hostContext);
        Assert.NotNull(environment);
        Assert.Equal(environment, hostContext.Environment);
        Assert.Equal("./test-plugin-configs", environment.PluginSettingsRootPath);
    }
}