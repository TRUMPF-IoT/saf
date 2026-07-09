// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Abstractions;
using Testably.Abstractions;

public static class HostApplicationBuilderExtensions
{
    public static IPluginSystemHostBuilder AddPluginSystem(this IHostApplicationBuilder hostAppBuilder, Action<PluginSystemOptions> configure)
    {
        var pluginHostBuilder = new PluginSystemHostBuilder(hostAppBuilder);

        pluginHostBuilder.Services.Configure(configure);
        pluginHostBuilder.Services.AddSingleton<IFileSystem, RealFileSystem>();
        pluginHostBuilder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PluginSystemOptions>>();
            if (!string.IsNullOrWhiteSpace(options.Value.PluginSettingsRootPath))
            {
                pluginHostBuilder.Environment.PluginSettingsRootPath = Environment.ExpandEnvironmentVariables(options.Value.PluginSettingsRootPath);
            }
            return pluginHostBuilder.Environment;
        });
        pluginHostBuilder.Services.AddSingleton<IPluginSystemHostContext>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PluginSystemOptions>>();
            var environment = sp.GetRequiredService<IPluginSystemHostEnvironment>();
            var logger = sp.GetRequiredService<ILogger<PluginSystemHostContext>>();
            var fileSystem = sp.GetRequiredService<IFileSystem>();
            return new PluginSystemHostContext(logger, environment, hostAppBuilder.Configuration, options.Value, fileSystem);
        });

        pluginHostBuilder.Services.AddSingleton<IPublicServiceTypeRegistry, PublicServiceTypeRegistry>();
        pluginHostBuilder.Services.AddSingleton<IPluginManifestLoader, PluginManifestLoader>();
        pluginHostBuilder.Services.AddSingleton<IPluginServicesContainer, PluginServicesContainer>();
        pluginHostBuilder.Services.AddSingleton<IPluginServiceProvider, PluginServiceProvider>();

        hostAppBuilder.Services.AddHostedService<ServicePluginHost>();

        return pluginHostBuilder;
    }
}