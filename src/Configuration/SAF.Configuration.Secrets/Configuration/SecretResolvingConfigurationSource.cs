// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using Microsoft.Extensions.Configuration;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// The configuration source that produces a <see cref="SecretResolvingConfigurationProvider"/>. It
/// captures the configuration sources present when it was added so the provider can read the values it
/// needs to resolve (its own value overrides are applied last).
/// </summary>
internal sealed class SecretResolvingConfigurationSource(
    IEnumerable<IConfigurationSource> innerSources,
    Action<SecretStoreOptions>? configure,
    Action<ISecretStoreBuilder>? configureProviders,
    HostSecretStoreAccessor? accessor) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
        => new SecretResolvingConfigurationProvider(innerSources, configure, configureProviders, accessor);
}
