// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Contracts;

/// <summary>
/// Interface that is used to search for service assemblies and to load their service assembly manifests.
/// A service assembly search is used to discover service assemblies that are not directly added to the service assembly manager
/// but are located in a specific directory that can be added by calling AddServiceAssemblySearch.
/// </summary>
public interface IServiceAssemblySearch
{
    /// <summary>
    /// Searches for service assemblies in the configured directory and loads their service assembly manifests.
    /// </summary>
    /// <returns>An enumeration of the found and loaded <see cref="IServiceAssemblyManifest"/> instances.</returns>
    IEnumerable<IServiceAssemblyManifest> LoadServiceAssemblyManifests();
}