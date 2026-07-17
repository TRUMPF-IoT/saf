// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

/// <inheritdoc />
public class PluginSystemHostContext(
    ILogger<PluginSystemHostContext> logger,
    IPluginSystemHostEnvironment environment,
    IConfigurationManager hostConfiguration,
    PluginSystemOptions options,
    IFileSystem fileSystem,
    IEnumerable<Action<IConfigurationBuilder>>? configurePluginConfigurationSources = null) : IPluginSystemHostContext
{
    public IPluginSystemHostEnvironment Environment { get; } = environment;
    public IConfiguration HostConfiguration { get; } = hostConfiguration;
    public IConfiguration PluginConfiguration { get; } = BuildPluginConfiguration(
        logger,
        options,
        environment,
        fileSystem,
        configurePluginConfigurationSources ?? []);

    private static IConfiguration BuildPluginConfiguration(
        ILogger logger,
        PluginSystemOptions options,
        IPluginSystemHostEnvironment environment,
        IFileSystem fileSystem,
        IEnumerable<Action<IConfigurationBuilder>> configurePluginConfigurationSources)
    {
        var builder = new ConfigurationBuilder();

        AddDefaultPluginConfigurationSources(builder, logger, options, environment, fileSystem);
        AddCustomPluginConfigurationSources(builder, configurePluginConfigurationSources);

        return builder.Build();
    }

    private static void AddDefaultPluginConfigurationSources(
        IConfigurationBuilder builder,
        ILogger logger,
        PluginSystemOptions options,
        IPluginSystemHostEnvironment environment,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (string.IsNullOrEmpty(options.PluginSettingsFilePath))
        {
            logger.LogInformation("No plugin configuration file configured.");
            return;
        }

        var settingsFilePath = CreateAbsolutePath(fileSystem, AppContext.BaseDirectory, fileSystem.Path.Combine(environment.PluginSettingsRootPath, options.PluginSettingsFilePath));

        logger.LogDebug("Resolved path to plugin settings file from {PluginSettingsFilePath} to {PluginSettingsFileFullName}",
            options.PluginSettingsFilePath, settingsFilePath);

        if (!fileSystem.File.Exists(settingsFilePath))
        {
            logger.LogInformation("Plugin configuration file not found: {PluginSettingsFilePath}, {PluginSettingsFileFullName}",
                options.PluginSettingsFilePath, settingsFilePath);
        }

        builder.AddJsonFile(settingsFilePath, optional: true, reloadOnChange: true);

        var filePath = fileSystem.Path.Combine(fileSystem.Path.GetDirectoryName(settingsFilePath)!, fileSystem.Path.GetFileNameWithoutExtension(settingsFilePath));
        var fileExt = fileSystem.Path.GetExtension(settingsFilePath);
        var environmentSettingsFilePath = $"{filePath}.{environment.EnvironmentName}{fileExt}";
        builder.AddJsonFile(environmentSettingsFilePath, optional: true, reloadOnChange: true);
    }

    private static void AddCustomPluginConfigurationSources(
        IConfigurationBuilder builder,
        IEnumerable<Action<IConfigurationBuilder>> configurePluginConfigurationSources)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configurePluginConfigurationSources);

        foreach (var configureSource in configurePluginConfigurationSources)
        {
            configureSource(builder);
        }
    }

    private static string CreateAbsolutePath(IFileSystem fileSystem, string basePath, string path)
    {
        if (fileSystem.Path.IsPathRooted(path))
        {
            return path;
        }

        var result = fileSystem.Path.Combine(basePath, path);
        result = fileSystem.Path.GetFullPath(result);

        return result;
    }
}