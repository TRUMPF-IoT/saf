// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

internal interface IPluginServicesReloader
{
    ValueTask ReinitializeAsync(CancellationToken cancellationToken = default);
}
