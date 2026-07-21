// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Binds the <see cref="HostSecretStoreAccessor"/> to the host service provider on startup, switching
/// configuration secret resolution from the bootstrap reader to the host's <see cref="Contracts.ISecretStore"/>.
/// Ordering relative to other hosted services does not matter: binding fires the accessor's change
/// token, which makes the resolver reload regardless of when the binding happens.
/// </summary>
internal sealed class HostSecretStoreAccessorInitializer(HostSecretStoreAccessor accessor, IServiceProvider hostServices)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        accessor.Bind(hostServices);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
