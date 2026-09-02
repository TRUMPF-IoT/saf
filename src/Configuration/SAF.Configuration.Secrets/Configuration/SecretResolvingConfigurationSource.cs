// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using Microsoft.Extensions.Configuration;
using SAF.Configuration.Secrets;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// The configuration source that produces a <see cref="SecretResolvingConfigurationProvider"/>. The
/// configuration the provider resolves against is either built from the other sources of the same builder
/// (the <c>AddResolvedSecrets</c> path) or supplied as an already built root (the chained path, see
/// <see cref="SecretConfigurationRootExtensions"/>).
/// </summary>
internal sealed class SecretResolvingConfigurationSource(
    IConfiguration? chainedRoot,
    Action<SecretStoreOptions>? configure,
    Action<ISecretStoreBuilder>? configureProviders,
    IServiceProvider? hostServices) : IConfigurationSource
{
    public SecretResolvingConfigurationSource(
        Action<SecretStoreOptions>? configure,
        Action<ISecretStoreBuilder>? configureProviders,
        IServiceProvider? hostServices)
        : this(chainedRoot: null, configure, configureProviders, hostServices)
    {
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (chainedRoot is not null)
        {
            // Nothing can shadow the resolver here: it is the last provider of a configuration built for
            // exactly that purpose.
            return new SecretResolvingConfigurationProvider(
                chainedRoot, [], isChainedRoot: true, configure, configureProviders, hostServices);
        }

        var inner = BuildInner(builder);
        var precedingProviderCount = builder.Sources
            .TakeWhile(source => !ReferenceEquals(source, this))
            .Count(source => source is not SecretResolvingConfigurationSource);

        return new SecretResolvingConfigurationProvider(
            inner,
            inner.Providers.Skip(precedingProviderCount).ToList(),
            isChainedRoot: false,
            configure,
            configureProviders,
            hostServices);
    }

    // The sources are read here, at Build() time, and not when this source was added: a source registered
    // afterwards still has to be resolved, and being later in the chain it would win TryGet - the consumer
    // would receive the literal secret:// token as its value.
    private static IConfigurationRoot BuildInner(IConfigurationBuilder builder)
    {
        var innerBuilder = new ConfigurationBuilder();

        // The properties carry the builder's default file provider. Without them a source built here
        // before the outer builder gets to it falls back to FileConfigurationSource.EnsureDefaults(),
        // which creates a PhysicalFileProvider over the base directory - a recursive file watcher that
        // nothing disposes.
        foreach (var property in builder.Properties)
        {
            innerBuilder.Properties[property.Key] = property.Value;
        }

        foreach (var source in builder.Sources)
        {
            // Every resolving source is skipped, not just this one: two AddResolvedSecrets calls on one
            // builder would otherwise build each other recursively.
            if (source is not SecretResolvingConfigurationSource)
            {
                innerBuilder.Add(source);
            }
        }

        return innerBuilder.Build();
    }
}
