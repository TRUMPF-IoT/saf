// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde.Diagnostics;

using SAF.PluginSystem.Hosting.Contracts;

internal sealed class CdeDiagnosticsServicePlugin(ServiceHostDiagnostics diagnostics) : IServicePlugin
{
    private readonly ServiceHostDiagnostics _diagnostics = diagnostics;

    public Task StartAsync(CancellationToken token)
        => _diagnostics.StartAsync(token);

    public Task StopAsync(CancellationToken token)
        => _diagnostics.StopAsync(token);
}
