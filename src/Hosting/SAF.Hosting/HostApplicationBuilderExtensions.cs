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
    private const string BuiltInPluginAssemblyPatterns = "SAF.Messaging.Runtime.dll";
    private const string BuiltInPluginContractsSearchPattern = "SAF.Common.dll;SAF.Messaging.Contracts.dll";

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
            options => ConfigurePluginSystem(options, builder.Configuration));

        builder.Services.AddServiceHostInfo(builder.Configuration.GetSection(ServiceHostSectionName).Bind);
        RegisterBuiltInPluginAssemblies(pluginSystemBuilder);

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

        var pluginSystemBuilder = builder.AddPluginSystem(options => ConfigurePluginSystem(options, configurePluginSystem));

        builder.Services.AddServiceHostInfo(builder.Configuration.GetSection(ServiceHostSectionName).Bind);
        RegisterBuiltInPluginAssemblies(pluginSystemBuilder);

        return CreateSafHostBuilder(pluginSystemBuilder, builder.Configuration);
    }

    private static void RegisterBuiltInPluginAssemblies(IPluginSystemHostBuilder pluginSystemBuilder)
    {
        ArgumentNullException.ThrowIfNull(pluginSystemBuilder);

        pluginSystemBuilder.AddPluginAssemblyFolderContainer(options =>
        {
            options.SearchRootPath = AppContext.BaseDirectory;
            options.Recursive = false;
            options.IncludePatterns = BuiltInPluginAssemblyPatterns;
            options.ExcludePatterns = string.Empty;
        });
    }

    private static void ConfigurePluginSystem(PluginSystemOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.GetSection(PluginSystemSectionName).Bind(options);
        ApplyBuiltInPluginSystemDefaults(options);
    }

    private static void ConfigurePluginSystem(PluginSystemOptions options, Action<PluginSystemOptions> configurePluginSystem)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configurePluginSystem);

        configurePluginSystem(options);
        ApplyBuiltInPluginSystemDefaults(options);
    }

    private static void ApplyBuiltInPluginSystemDefaults(PluginSystemOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.PluginContractsSearchPattern = MergePatterns(
            BuiltInPluginContractsSearchPattern,
            options.PluginContractsSearchPattern);
    }

    private static string MergePatterns(params string?[] patternGroups)
        => string.Join(";",
            patternGroups
                .Where(static patterns => !string.IsNullOrWhiteSpace(patterns))
                .SelectMany(static patterns => patterns!.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase));

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
