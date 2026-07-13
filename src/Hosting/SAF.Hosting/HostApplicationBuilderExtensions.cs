// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SAF.PluginSystem.Hosting;
using SAF.PluginSystem.Hosting.Contracts;

public static class HostApplicationBuilderExtensions
{
    private const string PluginSystemSectionName = "PluginSystem";
    private const string ServiceHostSectionName = "ServiceHost";

    /// <summary>
    /// Adds the SAF host using configuration from the default "PluginSystem" and "ServiceHost" sections.
    /// Diagnostics are enabled automatically when <c>ServiceHost:EnableDiagnostics</c> is <c>true</c>.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>An <see cref="ISafHostBuilder"/> for further SAF host configuration.</returns>
    public static ISafHostBuilder AddSafHost(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var pluginSystemBuilder = builder.AddPluginSystem(
            options => builder.Configuration.GetSection(PluginSystemSectionName).Bind(options));

        builder.Services.AddServiceHostInfo(builder.Configuration);
        pluginSystemBuilder.AddPluginAssemblyFolderContainer(options =>
        {
            options.SearchRootPath = AppContext.BaseDirectory;
            options.Recursive = false;
            options.IncludePatterns = "SAF.Hosting.dll";
            options.ExcludePatterns = string.Empty;
        });

        return CreateSafHostBuilder(pluginSystemBuilder, builder.Configuration);
    }

    /// <summary>
    /// Adds the SAF host with explicit plugin system configuration. Host info is bound from the "ServiceHost" section.
    /// Diagnostics are enabled automatically when <c>ServiceHost:EnableDiagnostics</c> is <c>true</c>.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configurePluginSystem">Callback to configure the plugin system.</param>
    /// <returns>An <see cref="ISafHostBuilder"/> for further SAF host configuration.</returns>
    public static ISafHostBuilder AddSafHost(
        this IHostApplicationBuilder builder,
        Action<PluginSystemOptions> configurePluginSystem)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurePluginSystem);

        var pluginSystemBuilder = builder.AddPluginSystem(configurePluginSystem);

        builder.Services.AddServiceHostInfo(builder.Configuration);
        pluginSystemBuilder.AddPluginAssemblyFolderContainer(options =>
        {
            options.SearchRootPath = AppContext.BaseDirectory;
            options.Recursive = false;
            options.IncludePatterns = "SAF.Hosting.dll";
            options.ExcludePatterns = string.Empty;
        });

        return CreateSafHostBuilder(pluginSystemBuilder, builder.Configuration);
    }

    private static SafHostBuilder CreateSafHostBuilder(
        IPluginSystemHostBuilder pluginSystemBuilder,
        IConfiguration configuration)
    {
        var safHostBuilder = new SafHostBuilder(pluginSystemBuilder);

        var options = new ServiceHostOptions();
        configuration.GetSection(ServiceHostSectionName).Bind(options);

        if (options.EnableDiagnostics)
            safHostBuilder.AddHostDiagnostics();

        return safHostBuilder;
    }
}
