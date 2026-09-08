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
        ThrowIfBuiltEagerly(builder);

        builder.Add(new SecretResolvingConfigurationSource(configure, configureProviders, hostServices));
        return builder;
    }

    // A builder that is its own configuration root - ConfigurationManager, behind
    // Host.CreateApplicationBuilder().Configuration - builds every source as it is added. The resolving
    // source would then be built before the sources that follow it exist, so a reference from one of them
    // is neither resolved nor detectable as shadowing, and the consumer receives the literal token as its
    // credential. Refuse at registration rather than fail open at read time.
    private static void ThrowIfBuiltEagerly(IConfigurationBuilder builder)
    {
        if (builder is not IConfigurationRoot)
        {
            return;
        }

        throw new NotSupportedException(
            $"'{builder.GetType().Name}' builds every configuration source as soon as it is added, so " +
            $"{nameof(AddResolvedSecrets)} cannot see the sources added after it: a secret reference from " +
            "one of them would reach the consumer as its literal token instead of being resolved or " +
            $"reported. Compose the sources in a {nameof(ConfigurationBuilder)}, call " +
            $"{nameof(AddResolvedSecrets)} on that, and add the built root here - or use " +
            "SAF.Configuration.Secrets.Extensions, which resolves against the composed configuration root.");
    }
}
