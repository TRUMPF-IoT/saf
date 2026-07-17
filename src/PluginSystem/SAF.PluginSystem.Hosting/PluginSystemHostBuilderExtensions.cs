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
    /// <param name="configureSource">The callback that appends one or more providers to the plugin configuration builder.</param>
    /// <returns>The same <see cref="IPluginSystemHostBuilder"/> instance for chaining.</returns>
    public static IPluginSystemHostBuilder AddPluginConfigurationSource(
        this IPluginSystemHostBuilder hostBuilder,
        Action<IConfigurationBuilder> configureSource)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        ArgumentNullException.ThrowIfNull(configureSource);

        hostBuilder.Services.Configure<PluginConfigurationSourcesOptions>(options =>
            options.ConfigureSources.Add(configureSource));

        return hostBuilder;
    }

    public static IPluginSystemHostBuilder AddPluginAssemblyFolderContainer(this IPluginSystemHostBuilder hostBuilder)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        return hostBuilder.AddPluginAssemblyFolderContainer(_ => { });
    }

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
                sp.GetServices<IPluginAssemblyValidator>(),
                sp.GetRequiredService<IFileSystem>());
        });

        return hostBuilder;
    }

    /// <summary>
    /// Registers an additional plugin assembly validator in the execution chain.
    /// Validators are executed in registration order.
    /// </summary>
    public static IPluginSystemHostBuilder AddPluginAssemblyValidator<TValidator>(this IPluginSystemHostBuilder hostBuilder)
        where TValidator : class, IPluginAssemblyValidator
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        hostBuilder.Services.AddSingleton<IPluginAssemblyValidator, TValidator>();
        return hostBuilder;
    }

    /// <summary>
    /// Registers SAF's built-in strong-name plugin assembly validator.
    /// </summary>
    public static IPluginSystemHostBuilder AddStrongNamePluginAssemblyValidator(
        this IPluginSystemHostBuilder hostBuilder,
        Action<StrongNamePluginAssemblyValidatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        var options = new StrongNamePluginAssemblyValidatorOptions();
        configure?.Invoke(options);

        hostBuilder.Services.AddSingleton<IPluginAssemblyValidator>(_ => new StrongNamePluginAssemblyValidator(options));
        return hostBuilder;
    }

    /// <summary>
    /// Registers SAF's built-in digital-signature plugin assembly validator.
    /// </summary>
    public static IPluginSystemHostBuilder AddDigitalSignaturePluginAssemblyValidator(
        this IPluginSystemHostBuilder hostBuilder,
        Action<DigitalSignaturePluginAssemblyValidatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        var options = new DigitalSignaturePluginAssemblyValidatorOptions();
        configure?.Invoke(options);

        hostBuilder.Services.AddSingleton<IPluginAssemblyValidator>(_ => new DigitalSignaturePluginAssemblyValidator(options));
        return hostBuilder;
    }
}