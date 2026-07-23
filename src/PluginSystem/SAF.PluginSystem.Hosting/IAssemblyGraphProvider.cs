// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using System.Reflection;

/// <summary>
/// Resolves an <see cref="AssemblyName"/> against the assemblies the host provides and exposes its
/// direct references, so that the transitive closure of the plugin contracts can be computed without the
/// registry taking a hard dependency on the runtime loader.
/// </summary>
internal interface IAssemblyGraphProvider
{
    /// <summary>
    /// Resolves the given name to the assembly the host actually provides (applying version roll-forward)
    /// and returns its reference graph node.
    /// </summary>
    /// <param name="assemblyName">The requested assembly name.</param>
    /// <returns>The resolved node, or <see langword="null"/> if the host does not provide the assembly.</returns>
    AssemblyGraphNode? TryResolve(AssemblyName assemblyName);
}
