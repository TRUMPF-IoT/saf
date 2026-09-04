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
        pluginSystemHostBuilder.AddPluginConfigurationSource(sourceContext =>
            sourceContext.Builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Custom:Setting"] = "ValueFromOutside",
            }));

        using var serviceProvider = services.BuildServiceProvider();
        var hostContext = serviceProvider.GetRequiredService<IPluginSystemHostContext>();

        // Assert
        Assert.Equal("ValueFromOutside", hostContext.PluginConfiguration["Custom:Setting"]);
    }

    [Fact]
    public void AddPluginSystem_LetsAConfigurationSourceResolveAHostService()
    {
        var services = new ServiceCollection();
        var builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);
        services.AddLogging();
        services.AddSingleton(new HostRegisteredService());

        var pluginSystemHostBuilder = builder.AddPluginSystem(options => options.PluginSettingsFilePath = string.Empty);
        pluginSystemHostBuilder.AddPluginConfigurationSource(sourceContext =>
            sourceContext.Builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Custom:Setting"] = sourceContext.HostServices.GetRequiredService<HostRegisteredService>().Value,
            }));

        using var serviceProvider = services.BuildServiceProvider();
        var hostContext = serviceProvider.GetRequiredService<IPluginSystemHostContext>();

        Assert.Equal("ValueFromTheHostContainer", hostContext.PluginConfiguration["Custom:Setting"]);
    }

    [Fact]
    public void AddPluginSystem_RefusesToResolveAPluginSystemServiceFromAConfigurationSource()
    {
        // IPluginServiceProvider needs IPluginServicesContainer, which needs IPluginSystemHostContext -
        // the service whose factory these callbacks run inside. The container does not report that cycle:
        // it re-invokes the factory it is already in until the process dies of an uncatchable
        // StackOverflowException, with no exception and no log line.
        var services = new ServiceCollection();
        var builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);
        services.AddLogging();

        var pluginSystemHostBuilder = builder.AddPluginSystem(options => options.PluginSettingsFilePath = string.Empty);
        pluginSystemHostBuilder.AddPluginConfigurationSource(
            sourceContext => sourceContext.HostServices.GetRequiredService<IPluginServiceProvider>());

        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => serviceProvider.GetRequiredService<IPluginSystemHostContext>());
        Assert.Contains(typeof(IPluginServiceProvider).ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(IPluginSystemHostContext), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(PluginConfigurationSourceContext.HostServices), exception.Message, StringComparison.Ordinal);
    }

    private sealed class HostRegisteredService
    {
        public string Value => "ValueFromTheHostContainer";
    }
}