// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        pluginHostBuilder.Services.TryAddSingleton<IFileSystem, RealFileSystem>();
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
            var configureSources = sp.GetRequiredService<IOptions<PluginConfigurationSourcesOptions>>().Value.ConfigureSources;
            return new PluginSystemHostContext(logger, environment, hostAppBuilder.Configuration, options.Value, fileSystem, configureSources);
        });

        pluginHostBuilder.Services.AddSingleton<IPublicServiceTypeRegistry, PublicServiceTypeRegistry>();
        pluginHostBuilder.Services.AddSingleton<IPluginManifestLoader, PluginManifestLoader>();
        pluginHostBuilder.Services.AddSingleton<PluginServicesContainer>();
        pluginHostBuilder.Services.AddSingleton<IPluginServicesContainer>(sp => sp.GetRequiredService<PluginServicesContainer>());
        pluginHostBuilder.Services.AddSingleton<IPluginServicesReloader>(sp => sp.GetRequiredService<PluginServicesContainer>());
        pluginHostBuilder.Services.AddSingleton<IPluginServiceProvider, PluginServiceProvider>();
        pluginHostBuilder.Services.AddSingleton<IServicePluginLifecycleRunner, ServicePluginLifecycleRunner>();
        pluginHostBuilder.Services.AddSingleton<IPluginSystemController, PluginSystemController>();

        hostAppBuilder.Services.AddHostedService<ServicePluginHost>();

        return pluginHostBuilder;
    }
}