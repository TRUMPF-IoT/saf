// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

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
        Assert.Contains(services, s => s.ServiceType == typeof(IPluginSystemController));
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

        using var serviceProvider = services.BuildServiceProvider();
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

        builder.AddPluginSystem(sp => sp.PluginSettingsRootPath = "./test-plugin-configs");

        using var serviceProvider = services.BuildServiceProvider();
        var hostContext = serviceProvider.GetRequiredService<IPluginSystemHostContext>();
        var environment = serviceProvider.GetRequiredService<IPluginSystemHostEnvironment>();

        Assert.NotNull(hostContext);
        Assert.NotNull(environment);
        Assert.Equal(environment, hostContext.Environment);
        Assert.Equal("./test-plugin-configs", environment.PluginSettingsRootPath);
    }

    [Fact]
    public void AddPluginSystem_AllowsAddingCustomPluginConfigurationSourcesFromOutside()
    {
        // Arrange
        var services = new ServiceCollection();

        var builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);

        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Is(nameof(PluginSystemHostContext)))
            .Returns(NullLogger<PluginSystemHostContext>.Instance);
        services.AddSingleton(loggerFactory);
        services.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        // Act
        var pluginSystemHostBuilder = builder.AddPluginSystem(options =>
        {
            options.PluginSettingsFilePath = string.Empty;
        });
        pluginSystemHostBuilder.AddPluginConfigurationSource(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Custom:Setting"] = "ValueFromOutside",
            }));

        var serviceProvider = services.BuildServiceProvider();
        var hostContext = serviceProvider.GetRequiredService<IPluginSystemHostContext>();

        // Assert
        Assert.Equal("ValueFromOutside", hostContext.PluginConfiguration["Custom:Setting"]);
    }
}