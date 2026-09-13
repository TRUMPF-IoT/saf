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
    /// Gets the assemblies this source requires to be shared. Each <see cref="AssemblyName"/> should carry
    /// a <see cref="AssemblyName.Version"/> - <c>typeof(T).Assembly.GetName()</c> always provides one, but a
    /// hand-written <see cref="AssemblyName(string)"/> built from just the simple name does not. If the
    /// version is missing, the registry falls back to an already-loaded assembly of the same simple name,
    /// or otherwise ignores the entry and logs a warning.
    /// </summary>
    IEnumerable<AssemblyName> GetSharedAssemblyNames();
}
