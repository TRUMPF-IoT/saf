// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
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
    IFileSystem fileSystem) : IPluginSystemHostContext
{
    public IPluginSystemHostEnvironment Environment { get; } = environment;
    public IConfiguration HostConfiguration { get; } = hostConfiguration;
    public IConfiguration PluginConfiguration { get; } = BuildPluginConfiguration(logger, options, environment, fileSystem);

    private static IConfiguration BuildPluginConfiguration(
        ILogger logger,
        PluginSystemOptions options,
        IPluginSystemHostEnvironment environment,
        IFileSystem fileSystem)
    {
        var builder = new ConfigurationBuilder();

        if (string.IsNullOrEmpty(options.PluginSettingsFilePath))
        {
            logger.LogInformation("No plugin configuration file configured.");
        }
        else
        {
            var settingsFilePath = CreateAbsolutePath(fileSystem, AppContext.BaseDirectory, fileSystem.Path.Combine(environment.PluginSettingsRootPath, options.PluginSettingsFilePath));

            logger.LogDebug("Resolved path to plugin settings file from {PluginSettingsFilePath} to {PluginSettingsFileFullName}",
                options.PluginSettingsFilePath, settingsFilePath);

            if (!fileSystem.File.Exists(settingsFilePath))
            {
                logger.LogInformation("Plugin configuration file not found: {PluginSettingsFilePath}, {PluginSettingsFileFullName}",
                    options.PluginSettingsFilePath, settingsFilePath);
            }

            builder.AddJsonFile(settingsFilePath, true);

            var filePath = fileSystem.Path.Combine(fileSystem.Path.GetDirectoryName(settingsFilePath)!, fileSystem.Path.GetFileNameWithoutExtension(settingsFilePath));
            var fileExt = fileSystem.Path.GetExtension(settingsFilePath);
            var environmentSettingsFilePath = $"{filePath}.{environment.EnvironmentName}{fileExt}";
            builder.AddJsonFile(environmentSettingsFilePath, true);
        }

        return builder.Build();
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