// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde.Diagnostics;

using Microsoft.Extensions.DependencyInjection;
using SAF.PluginSystem.Hosting.Contracts;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CDE diagnostics as an optional plugin lifecycle service.
    /// </summary>
    /// <param name="collection">The plugin service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddCdeDiagnostics(this IServiceCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        collection.AddSingleton<ServiceHostDiagnostics>();
        collection.AddServicePlugin<CdeDiagnosticsServicePlugin>();
        return collection;
    }
}
