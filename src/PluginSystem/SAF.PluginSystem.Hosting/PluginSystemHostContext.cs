// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

/// <inheritdoc />
public sealed class PluginSystemHostContext : IPluginSystemHostContext, IDisposable
{
    private readonly IConfigurationRoot _pluginConfigurationRoot;
    private readonly PhysicalFileProvider? _pluginSettingsFileProvider;

    public PluginSystemHostContext(
        ILogger<PluginSystemHostContext> logger,
        IPluginSystemHostEnvironment environment,
        IConfigurationManager hostConfiguration,
        PluginSystemOptions options,
        IFileSystem fileSystem)
    {
        Environment = environment;
        HostConfiguration = hostConfiguration;

        (_pluginConfigurationRoot, _pluginSettingsFileProvider) = BuildPluginConfiguration(logger, options, environment, fileSystem);
    }

    public IPluginSystemHostEnvironment Environment { get; }
    public IConfiguration HostConfiguration { get; }
    public IConfiguration PluginConfiguration => _pluginConfigurationRoot;

    public void Dispose()
    {
        if (_pluginConfigurationRoot is IDisposable disposableConfigurationRoot)
        {
            disposableConfigurationRoot.Dispose();
        }

        _pluginSettingsFileProvider?.Dispose();
    }

    private static (IConfigurationRoot ConfigurationRoot, PhysicalFileProvider? SettingsFileProvider) BuildPluginConfiguration(
        ILogger logger,
        PluginSystemOptions options,
        IPluginSystemHostEnvironment environment,
        IFileSystem fileSystem)
    {
        var builder = new ConfigurationBuilder();

        if (string.IsNullOrEmpty(options.PluginSettingsFilePath))
        {
            logger.LogInformation("No plugin configuration file configured.");
            return (builder.Build(), null);
        }

        var settingsFilePath = CreateAbsolutePath(fileSystem, AppContext.BaseDirectory, fileSystem.Path.Combine(environment.PluginSettingsRootPath, options.PluginSettingsFilePath));

        logger.LogDebug("Resolved path to plugin settings file from {PluginSettingsFilePath} to {PluginSettingsFileFullName}",
            options.PluginSettingsFilePath, settingsFilePath);

        var settingsDirectoryPath = fileSystem.Path.GetDirectoryName(settingsFilePath) ?? AppContext.BaseDirectory;
        if (!fileSystem.Directory.Exists(settingsDirectoryPath))
        {
            fileSystem.Directory.CreateDirectory(settingsDirectoryPath);
        }

        var settingsFileProvider = new PhysicalFileProvider(settingsDirectoryPath);
        var settingsFileName = fileSystem.Path.GetFileName(settingsFilePath);

        if (!fileSystem.File.Exists(settingsFilePath))
        {
            logger.LogInformation("Plugin configuration file not found: {PluginSettingsFilePath}, {PluginSettingsFileFullName}",
                options.PluginSettingsFilePath, settingsFilePath);
        }

        builder.AddJsonFile(settingsFileProvider, settingsFileName, optional: true, reloadOnChange: true);

        var environmentSettingsFileName = $"{fileSystem.Path.GetFileNameWithoutExtension(settingsFileName)}.{environment.EnvironmentName}{fileSystem.Path.GetExtension(settingsFileName)}";
        builder.AddJsonFile(settingsFileProvider, environmentSettingsFileName, optional: true, reloadOnChange: true);

        return (builder.Build(), settingsFileProvider);
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