// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using Microsoft.Extensions.Configuration;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// Adds transparent secret resolution to a configuration builder.
/// </summary>
public static class SecretConfigurationBuilderExtensions
{
    /// <summary>
    /// Appends a source that resolves secret references (values starting with
    /// <see cref="SecretStoreOptions.ReferencePrefix"/>) already present in the builder. The resolving
    /// source reads the configuration sources added <em>before</em> this call and overrides only the
    /// values that are references, leaving all other values untouched.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <param name="configure">An optional callback to configure <see cref="SecretStoreOptions"/>.</param>
    /// <param name="configureProviders">
    /// An optional callback to register providers explicitly, in priority order. When omitted, all
    /// built-in providers for the current platform are registered.
    /// </param>
    /// <returns>The same <see cref="IConfigurationBuilder"/> instance for chaining.</returns>
    public static IConfigurationBuilder AddResolvedSecrets(
        this IConfigurationBuilder builder,
        Action<SecretStoreOptions>? configure = null,
        Action<ISecretStoreBuilder>? configureProviders = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddResolvedSecrets(hostServices: null, configure, configureProviders);
    }

    /// <summary>
    /// Internal overload that additionally passes the host <see cref="IServiceProvider"/> (available while
    /// plugin configuration is being built), so the resolver reads the reader and options directly from it
    /// instead of building a self-contained one.
    /// </summary>
    internal static IConfigurationBuilder AddResolvedSecrets(
        this IConfigurationBuilder builder,
        IServiceProvider? hostServices,
        Action<SecretStoreOptions>? configure = null,
        Action<ISecretStoreBuilder>? configureProviders = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var innerSources = builder.Sources.ToList();
        builder.Add(new SecretResolvingConfigurationSource(innerSources, configure, configureProviders, hostServices));
        return builder;
    }
}
