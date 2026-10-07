// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.Cde;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAF.Cde.Common;
using SAF.Common;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the C-DEngine based storage infrastructure.
    /// </summary>
    /// <remarks>
    /// The storage runs on the C-DEngine node of the process and takes a lease on it, so register the node
    /// with <c>AddCde</c> as well.
    /// </remarks>
    /// <param name="collection">The service collection to register services in.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCdeStorageInfrastructure(this IServiceCollection collection)
        => collection.AddSingleton<IStorageInfrastructure, Storage>(sp =>
        {
            _ = sp.GetRequiredService<CdeNodeLease>();
            return new Storage(sp.GetService<ILogger<Storage>>());
        });
}