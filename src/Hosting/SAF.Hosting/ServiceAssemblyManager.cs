// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Microsoft.Extensions.Logging;
using Contracts;

internal class ServiceAssemblyManager(ILogger<ServiceAssemblyManager> logger,
        IServiceAssemblySearch? assemblySearch,
        IEnumerable<IServiceAssemblyManifest> serviceAssemblies)
    : IServiceAssemblyManager
{
    public IEnumerable<IServiceAssemblyManifest> GetServiceAssemblyManifests()
    {
        var loadedAssemblies = assemblySearch?.LoadServiceAssemblyManifests();

        logger.LogInformation("Registered {AssemblyCount} assemblies.", serviceAssemblies.Count());
        return serviceAssemblies
            .Concat(loadedAssemblies ?? [])
            .ToList();
    }
}