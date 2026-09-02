// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using Microsoft.Extensions.Configuration;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// Wraps an already built configuration root in one that resolves secret references.
/// </summary>
internal static class SecretConfigurationRootExtensions
{
    /// <summary>
    /// Returns a configuration root that serves <paramref name="inner"/> with every value that is a secret
    /// reference replaced by the resolved secret. Ownership of <paramref name="inner"/> passes to the
    /// returned root: disposing it disposes <paramref name="inner"/>.
    /// </summary>
    /// <remarks>
    /// Unlike <c>AddResolvedSecrets</c>, which has to build a second copy of the sources it resolves
    /// against, this chains the root that already exists - so every file is parsed and watched once.
    /// It needs the composed configuration, which only exists once every source has been built.
    /// </remarks>
    public static IConfigurationRoot ResolveSecrets(
        this IConfigurationRoot inner,
        IServiceProvider? hostServices,
        Action<SecretStoreOptions>? configure = null,
        Action<ISecretStoreBuilder>? configureProviders = null)
    {
        ArgumentNullException.ThrowIfNull(inner);

        return new ConfigurationBuilder()
            .Add(new ChainedConfigurationSource
            {
                Configuration = new UnwatchedConfigurationRoot(inner),
                ShouldDisposeConfiguration = true
            })
            .Add(new SecretResolvingConfigurationSource(inner, configure, configureProviders, hostServices))
            .Build();
    }
}
