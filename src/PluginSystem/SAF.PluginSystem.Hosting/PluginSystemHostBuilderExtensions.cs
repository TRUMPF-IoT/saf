// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Abstractions;

public static class PluginSystemHostBuilderExtensions
{
    /// <summary>
    /// Appends a custom plugin configuration source to the plugin configuration builder pipeline.
    /// </summary>
    /// <param name="hostBuilder">The plugin system host builder.</param>
    /// <param name="configureSource">
    /// The callback that appends one or more providers to the plugin configuration builder. It receives a
    /// <see cref="PluginConfigurationSourceContext"/> giving access to the resolved plugin settings file
    /// provider, environment name, settings file name, and the shared load-exception handler, so sources
    /// that follow the same conventions as the built-in plugin settings file (e.g. an environment-specific
    /// overlay with a different extension) can be expressed in a single call.
    /// The callback runs exactly once, during <see cref="IPluginSystemHostContext"/> construction; any
    /// exception it throws propagates into host startup.
    /// </param>
    /// <returns>The same <see cref="IPluginSystemHostBuilder"/> instance for chaining.</returns>
    public static IPluginSystemHostBuilder AddPluginConfigurationSource(
        this IPluginSystemHostBuilder hostBuilder,
        Action<PluginConfigurationSourceContext> configureSource)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(configureSource);

        hostBuilder.Services.Configure<PluginConfigurationSourcesOptions>(options =>
            options.ConfigureSources.Add(configureSource));

        return hostBuilder;
    }

    /// <summary>
    /// Registers a plugin assembly container that discovers plugin assemblies by scanning a folder, using
    /// the default <see cref="PluginAssemblyFolderSearchOptions"/>.
    /// </summary>
    /// <param name="hostBuilder">The plugin system host builder.</param>
    /// <returns>The same <see cref="IPluginSystemHostBuilder"/> instance for chaining.</returns>
    public static IPluginSystemHostBuilder AddPluginAssemblyFolderContainer(this IPluginSystemHostBuilder hostBuilder)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        return hostBuilder.AddPluginAssemblyFolderContainer(_ => { });
    }

    /// <summary>
    /// Registers a plugin assembly container that discovers plugin assemblies by scanning a folder.
    /// </summary>
    /// <param name="hostBuilder">The plugin system host builder.</param>
    /// <param name="configureSearch">The callback that configures the folder search (root path, recursion, include/exclude patterns).</param>
    /// <returns>The same <see cref="IPluginSystemHostBuilder"/> instance for chaining.</returns>
    public static IPluginSystemHostBuilder AddPluginAssemblyFolderContainer(this IPluginSystemHostBuilder hostBuilder, Action<PluginAssemblyFolderSearchOptions> configureSearch)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        var uniqueOptionsKey = $"{nameof(PluginAssemblyFolderSearchOptions)}_{Guid.NewGuid():N}";
        hostBuilder.Services.Configure(uniqueOptionsKey, configureSearch);
        hostBuilder.Services.AddSingleton<IPluginAssemblyContainer, PluginAssemblyFolderContainer>(sp =>
        {
            var namedOptionsAccessor = sp.GetRequiredService<IOptionsMonitor<PluginAssemblyFolderSearchOptions>>();
            var options = namedOptionsAccessor.Get(uniqueOptionsKey);

            return new PluginAssemblyFolderContainer(
                sp.GetRequiredService<ILoggerFactory>(),
                sp.GetRequiredService<IPluginManifestLoader>(),
                options,
                sp.GetRequiredService<IFileSystem>(),
                sp.GetServices<IPluginAssemblyValidator>());
        });

        return hostBuilder;
    }
}