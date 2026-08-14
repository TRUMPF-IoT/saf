// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Extensions;

using Microsoft.Extensions.DependencyInjection;
using SAF.Configuration.Secrets;
using SAF.Configuration.Secrets.Contracts;
using SAF.PluginSystem.Hosting;
using SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Integrates the secret store into the SAF plugin system host builder.
/// </summary>
public static class PluginSystemHostBuilderExtensions
{
    /// <summary>
    /// Registers the secret store and forwards the resolved <see cref="ISecretStore"/> into every plugin
    /// container, so plugins can inject it directly.
    /// </summary>
    /// <param name="hostBuilder">The plugin system host builder.</param>
    /// <param name="configure">An optional callback to configure <see cref="SecretStoreOptions"/>.</param>
    /// <param name="configureProviders">
    /// An optional callback to register providers explicitly, in priority order. When omitted, all
    /// built-in providers for the current platform are registered (see
    /// <see cref="SecretStoreBuilderExtensions.AddDefaults"/>).
    /// </param>
    /// <returns>The same <see cref="IPluginSystemHostBuilder"/> instance for chaining.</returns>
    public static IPluginSystemHostBuilder AddSecretStore(
        this IPluginSystemHostBuilder hostBuilder,
        Action<SecretStoreOptions>? configure = null,
        Action<ISecretStoreBuilder>? configureProviders = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        var storeBuilder = hostBuilder.Services.AddSecretStore(configure);
        if (configureProviders is null)
        {
            storeBuilder.AddDefaults();
        }
        else
        {
            configureProviders(storeBuilder);
        }

        // Bridge: forward the single ISecretStore into every plugin container. Runs before each plugin
        // manifest's ConfigureServices, so plugins always receive the same host-level secret store.
        hostBuilder.Services.AddSingleton<IHostServiceForwarder, HostServiceForwarder<ISecretStore>>();

        return hostBuilder;
    }

    /// <summary>
    /// Enables transparent secret resolution for the plugin configuration: values that are secret
    /// references (starting with <see cref="SecretStoreOptions.ReferencePrefix"/>) are replaced with the
    /// resolved secret when the configuration is read, so existing configuration-bound plug-ins receive
    /// the real value without code changes. Values that are not references are left untouched.
    /// </summary>
    /// <param name="hostBuilder">The plugin system host builder.</param>
    /// <param name="configure">An optional callback to configure <see cref="SecretStoreOptions"/>.</param>
    /// <param name="configureProviders">
    /// An optional callback to register providers explicitly, in priority order. When omitted, all
    /// built-in providers for the current platform are registered.
    /// </param>
    /// <returns>The same <see cref="IPluginSystemHostBuilder"/> instance for chaining.</returns>
    public static IPluginSystemHostBuilder AddSecretConfigurationResolution(
        this IPluginSystemHostBuilder hostBuilder,
        Action<SecretStoreOptions>? configure = null,
        Action<ISecretStoreBuilder>? configureProviders = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        // Register the host-container store with the same configuration, so that once the container is
        // available the resolver uses it. Provider registration is idempotent, so this composes with a
        // separate AddSecretStore call.
        var storeBuilder = hostBuilder.Services.AddSecretStore(configure);
        if (configureProviders is null)
        {
            storeBuilder.AddDefaults();
        }
        else
        {
            configureProviders(storeBuilder);
        }

        // Shared bridge: the resolver (built before the container) and the initializer (run once the
        // container exists) reference the same accessor instance.
        var accessor = new HostSecretStoreAccessor();
        hostBuilder.Services.AddSingleton(accessor);
        hostBuilder.Services.AddHostedService(sp => new HostSecretStoreAccessorInitializer(accessor, sp));

        hostBuilder.AddPluginConfigurationSource(
            source => source.Builder.AddResolvedSecrets(accessor, configure, configureProviders));
        return hostBuilder;
    }
}
