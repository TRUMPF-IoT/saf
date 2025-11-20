// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Contracts;

/// <summary>
/// Interface that is used to discover service assemblies that where added using the service assembly search or by directly adding them to the service assembly manager.
/// </summary>
public interface IServiceAssemblyManager
{
    /// <summary>
    /// Gets all found and registered service assembly manifests.
    /// </summary>
    /// <returns>The service assembly manifests.</returns>
    IEnumerable<IServiceAssemblyManifest> GetServiceAssemblyManifests();
}