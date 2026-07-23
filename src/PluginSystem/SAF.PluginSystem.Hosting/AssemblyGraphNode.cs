// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using System.Reflection;

/// <summary>
/// Represents a single node in the assembly reference graph: the resolved assembly identity as the host
/// provides it, together with the assemblies it directly references.
/// </summary>
/// <param name="Name">The resolved <see cref="AssemblyName"/> as provided by the host (after roll-forward).</param>
/// <param name="ReferencedAssemblies">The assemblies this assembly directly references.</param>
internal sealed record AssemblyGraphNode(AssemblyName Name, IReadOnlyList<AssemblyName> ReferencedAssemblies);
