// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// Bridges the host's dependency injection container to the configuration resolution provider, which
/// runs before the container exists. Until <see cref="Bind"/> is called the accessor is unbound and the
/// resolver falls back to its own bootstrap reader; once bound, the resolver switches to the host's
/// <see cref="ISecretStore"/> and re-resolves. A change token signals the transition so the resolver
/// can reload.
/// </summary>
internal sealed class HostSecretStoreAccessor
{
    private volatile IServiceProvider? _hostServices;
    private ConfigurationReloadToken _changeToken = new();

    /// <summary>
    /// Returns the host secret reader when the container has been bound; otherwise <see langword="false"/>.
    /// </summary>
    public bool TryGetReader([NotNullWhen(true)] out ISecretReader? reader)
    {
        var hostServices = _hostServices;
        if (hostServices is null)
        {
            reader = null;
            return false;
        }

        reader = hostServices.GetRequiredService<ISecretStore>();
        return true;
    }

    /// <summary>A token that fires once when the host container is bound.</summary>
    public IChangeToken GetChangeToken() => _changeToken;

    /// <summary>Binds the host service provider and signals the change token.</summary>
    public void Bind(IServiceProvider hostServices)
    {
        ArgumentNullException.ThrowIfNull(hostServices);

        _hostServices = hostServices;
        var previous = Interlocked.Exchange(ref _changeToken, new ConfigurationReloadToken());
        previous.OnReload();
    }
}
