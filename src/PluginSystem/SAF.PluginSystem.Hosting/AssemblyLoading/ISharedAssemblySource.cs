// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using System.Reflection;

/// <summary>
/// Contributes assemblies to the set shared between the host and every plugin load context.
/// Register one for any assembly whose types cross the plugin boundary but that the plugin system
/// cannot discover on its own - above all the contract assembly of a host service forwarded via
/// <see cref="Contracts.IHostServiceForwarder"/>.
/// </summary>
public interface ISharedAssemblySource
{
    /// <summary>
    /// Gets the assemblies this source requires to be shared.
    /// </summary>
    IEnumerable<AssemblyName> GetSharedAssemblyNames();
}
