// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SAF.Common;
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginManifestTests
{
    [Fact]
    public void ConfigureServices_RegistersIServiceHostInfo()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceHost:Id"] = "bridge-host-id",
                ["ServiceHost:ServiceHostType"] = "BridgeType",
                ["ServiceHost:FileSystemUserBasePath"] = "bridge-user-path",
                ["ServiceHost:FileSystemInstallationPath"] = "bridge-install-path"
            })
            .Build();

        var context = Substitute.For<IPluginSystemHostContext>();
        context.HostConfiguration.Returns(configuration);

        var services = new ServiceCollection();
        var manifest = new PluginManifest();

        manifest.ConfigureServices(context, services);

        var provider = services.BuildServiceProvider();
        var hostInfo = provider.GetRequiredService<IServiceHostInfo>();

        Assert.Equal("bridge-host-id", hostInfo.Id);
        Assert.Equal("BridgeType", hostInfo.ServiceHostType);
        Assert.Equal("bridge-user-path", hostInfo.FileSystemUserBasePath);
        Assert.Equal("bridge-install-path", hostInfo.FileSystemInstallationPath);
    }

    [Fact]
    public void AddSafHost_LoadsPluginManifestsFromDirectlyReferencedAssemblies()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddTransient(typeof(ILogger<>), typeof(Logger<>));

        var builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);
        builder.Configuration.Returns(new ConfigurationManager());

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Test");
        builder.Environment.Returns(environment);

        builder.AddSafHost();

        var provider = services.BuildServiceProvider();
        var pluginAssemblyContainers = provider.GetServices<IPluginAssemblyContainer>().ToList();
        var manifestTypeNames = pluginAssemblyContainers
            .SelectMany(static container => container.GetPluginManifests())
            .Select(static manifest => manifest.GetType().FullName)
            .ToList();

        Assert.Single(pluginAssemblyContainers);
        Assert.Contains("SAF.Hosting.PluginManifest", manifestTypeNames);
        Assert.Contains("SAF.Messaging.Runtime.PluginManifest", manifestTypeNames);
    }
}
