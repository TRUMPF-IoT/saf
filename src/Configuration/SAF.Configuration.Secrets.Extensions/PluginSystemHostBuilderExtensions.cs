// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SAF.Configuration.Secrets;
using SAF.Configuration.Secrets.Configuration;
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
    /// An optional callback to register providers explicitly, in priority order. When omitted, the
    /// built-in providers for the current platform are registered (see
    /// <see cref="SecretStoreBuilderExtensions.AddDefaults"/>) unless another call already registered
    /// providers, in which case those are used. Passing it when another call already registered providers
    /// throws; see the remarks on <see cref="AddSecretConfigurationResolution"/>.
    /// </param>
    /// <returns>The same <see cref="IPluginSystemHostBuilder"/> instance for chaining.</returns>
    public static IPluginSystemHostBuilder AddSecretStore(
        this IPluginSystemHostBuilder hostBuilder,
        Action<SecretStoreOptions>? configure = null,
        Action<ISecretStoreBuilder>? configureProviders = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        AddStore(hostBuilder.Services, configure, configureProviders, nameof(AddSecretStore));

        // Bridge: forward the single ISecretStore into every plugin container. Runs before each plugin
        // manifest's ConfigureServices, so plugins always receive the same host-level secret store.
        // TryAddEnumerable keeps a second call from forwarding the same store twice.
        hostBuilder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostServiceForwarder, HostServiceForwarder<ISecretStore>>());

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
    /// An optional callback to register providers explicitly, in priority order. Same rules as
    /// <see cref="AddSecretStore"/>: omitting it accepts the providers another call already registered, or
    /// registers the platform defaults if none did.
    /// </param>
    /// <returns>The same <see cref="IPluginSystemHostBuilder"/> instance for chaining.</returns>
    /// <remarks>
    /// Composes with <see cref="AddSecretStore"/>: both register the same store, calling either twice
    /// changes nothing, and options callbacks are applied in call order. What cannot compose is the
    /// provider list - the order it is registered in is the priority order auto-selection uses, and a
    /// second list would be appended behind the first rather than replace it - so passing
    /// <paramref name="configureProviders"/> to a second call throws <see cref="InvalidOperationException"/>
    /// instead of leaving the active backend to whichever call came first.
    /// </remarks>
    public static IPluginSystemHostBuilder AddSecretConfigurationResolution(
        this IPluginSystemHostBuilder hostBuilder,
        Action<SecretStoreOptions>? configure = null,
        Action<ISecretStoreBuilder>? configureProviders = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);

        // The resolver reads the options and the reader from the host container (see source.HostServices
        // below), so the store is registered here too - with the same options - whether or not the
        // consumer also calls AddSecretStore.
        var registration = AddStore(
            hostBuilder.Services, configure, configureProviders, nameof(AddSecretConfigurationResolution));
        if (registration.ResolutionRegistered)
        {
            return hostBuilder;
        }

        registration.ResolutionRegistered = true;

        // Decorating the root, rather than adding a source, is what lets the resolver read the composed
        // plugin configuration: every source is built by then, whichever callback added it, and the built
        // root is chained instead of rebuilt - so each settings file is parsed and watched once.
        hostBuilder.AddPluginConfigurationSource(
            source => source.DecorateConfigurationRoot(root => root.ResolveSecrets(source.HostServices)));
        return hostBuilder;
    }

    private static SecretStoreRegistration AddStore(
        IServiceCollection services,
        Action<SecretStoreOptions>? configure,
        Action<ISecretStoreBuilder>? configureProviders,
        string caller)
    {
        var registration = SecretStoreRegistration.GetOrAdd(services);
        var storeBuilder = services.AddSecretStore(configure);
        var owner = ProviderOwner(services, registration);

        if (configureProviders is null)
        {
            // Whatever is already registered stays. Appending the platform defaults on top cannot change
            // which provider is selected, and would add a fallback backend nobody asked for.
            if (owner is null)
            {
                storeBuilder.AddDefaults();
                registration.ProvidersConfiguredBy = $"an earlier {caller} call (the built-in defaults)";
            }

            return registration;
        }

        if (owner is not null)
        {
            throw new InvalidOperationException(
                $"Secret store providers were already configured by {owner}. The order they are registered "
                + $"in is the priority order auto-selection uses, so the providers passed to {caller} would "
                + "be appended behind the existing ones instead of replacing them, leaving the store reading "
                + $"from a backend that was not asked for. Register the providers once, and call {caller} "
                + "without configureProviders.");
        }

        configureProviders(storeBuilder);
        registration.ProvidersConfiguredBy = $"an earlier {caller} call";
        return registration;
    }

    // Providers registered without going through these methods - a direct
    // services.AddSecretStore().AddFile() - fix the priority order just as much.
    private static string? ProviderOwner(IServiceCollection services, SecretStoreRegistration registration)
        => registration.ProvidersConfiguredBy
            ?? (services.Any(descriptor => descriptor.ServiceType == typeof(ISecretStoreProvider))
                ? "an earlier secret store registration"
                : null);
}
