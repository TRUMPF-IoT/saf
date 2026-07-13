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
using SAF.PluginSystem.Hosting.Contracts;
using Xunit;

public class PluginManifestTests
{
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
        Assert.DoesNotContain("SAF.Hosting.PluginManifest", manifestTypeNames);
        Assert.Contains("SAF.Messaging.Runtime.PluginManifest", manifestTypeNames);
    }
}
