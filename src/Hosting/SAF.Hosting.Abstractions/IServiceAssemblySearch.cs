// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Abstractions;

public interface IServiceAssemblySearch
{
    IEnumerable<IServiceAssemblyManifest> LoadServiceAssemblyManifests();
}