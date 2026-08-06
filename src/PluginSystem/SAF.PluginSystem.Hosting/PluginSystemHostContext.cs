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
        IFileSystem fileSystem,
        IEnumerable<Action<IConfigurationBuilder>>? configurePluginConfigurationSources = null)
    {
        Environment = environment;
        HostConfiguration = hostConfiguration;

        (_pluginConfigurationRoot, _pluginSettingsFileProvider) =
            BuildPluginConfiguration(logger, options, environment, fileSystem, configurePluginConfigurationSources ?? []);
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

    private static (IConfigurationRoot configurationRoot, PhysicalFileProvider? settingsFileProvider) BuildPluginConfiguration(
        ILogger logger,
        PluginSystemOptions options,
        IPluginSystemHostEnvironment environment,
        IFileSystem fileSystem,
        IEnumerable<Action<IConfigurationBuilder>> configurePluginConfigurationSources)
    {
        var builder = new ConfigurationBuilder();

        var settingsFileProvider = AddDefaultPluginConfigurationSources(builder, logger, options, environment, fileSystem);
        AddCustomPluginConfigurationSources(builder, configurePluginConfigurationSources);

        return (builder.Build(), settingsFileProvider);
    }

    private static PhysicalFileProvider? AddDefaultPluginConfigurationSources(
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
            return null;
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

        AddJsonFile(builder, logger, settingsFileProvider, settingsFileName);

        var environmentSettingsFileName = $"{fileSystem.Path.GetFileNameWithoutExtension(settingsFileName)}.{environment.EnvironmentName}{fileSystem.Path.GetExtension(settingsFileName)}";
        AddJsonFile(builder, logger, settingsFileProvider, environmentSettingsFileName);

        return settingsFileProvider;
    }

    private static void AddCustomPluginConfigurationSources(
      IConfigurationBuilder builder,
      IEnumerable<Action<IConfigurationBuilder>> configurePluginConfigurationSources)
    {
        foreach (var configureSource in configurePluginConfigurationSources)
        {
            configureSource(builder);
        }
    }

    private static void AddJsonFile(IConfigurationBuilder builder, ILogger logger, PhysicalFileProvider settingsFileProvider, string settingsFileName)
        => builder.AddJsonFile(source =>
        {
            source.FileProvider = settingsFileProvider;
            source.Path = settingsFileName;
            source.Optional = true;
            source.ReloadOnChange = true;
            source.OnLoadException = context =>
            {
                context.Ignore = true;
                logger.LogWarning(context.Exception,
                    "Failed to load plugin configuration file {PluginSettingsFilePath}. A later successful reload will apply updated values.",
                    settingsFileName);
            };
        });

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