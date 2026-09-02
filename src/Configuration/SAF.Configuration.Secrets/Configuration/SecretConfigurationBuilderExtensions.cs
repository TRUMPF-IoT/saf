// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using Microsoft.Extensions.Configuration;
using SAF.Configuration.Secrets;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// Adds transparent secret resolution to a configuration builder.
/// </summary>
public static class SecretConfigurationBuilderExtensions
{
    /// <summary>
    /// Appends a source that resolves secret references (values starting with
    /// <see cref="SecretStoreOptions.ReferencePrefix"/>) in the configuration, overriding only the values
    /// that are references and leaving all other values untouched.
    /// </summary>
    /// <remarks>
    /// The sources to resolve are read when the builder is built, not when this method is called, so a
    /// source registered afterwards is resolved as well. The cost is that those sources are built twice -
    /// once for the builder, once for the resolver - because a provider can only read the composed
    /// configuration by composing it itself. Hosts that build the root themselves avoid this; see the
    /// plugin system integration in SAF.Configuration.Secrets.Extensions.
    /// </remarks>
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

        builder.Add(new SecretResolvingConfigurationSource(configure, configureProviders, hostServices));
        return builder;
    }
}
