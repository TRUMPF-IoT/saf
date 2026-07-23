// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using System.Reflection;

/// <summary>
/// Decides, for an assembly requested by an isolated plugin load context, whether it must be shared from
/// the default context, loaded in isolation, or represents an unresolvable version conflict.
/// </summary>
public interface ISharedAssemblyResolver
{
    /// <summary>
    /// Resolves the sharing decision for the requested assembly.
    /// </summary>
    /// <param name="requested">The assembly requested by the plugin load context.</param>
    /// <param name="hostVersion">
    /// The version the host provides for the assembly when it is part of the shared set; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <returns>The sharing decision.</returns>
    SharedAssemblyDecision Resolve(AssemblyName requested, out Version? hostVersion);
}
