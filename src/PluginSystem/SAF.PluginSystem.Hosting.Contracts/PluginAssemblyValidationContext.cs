// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

using System.Reflection;

/// <summary>
/// Provides metadata and a stable content snapshot for validating a plugin assembly candidate before loading it.
/// </summary>
public sealed class PluginAssemblyValidationContext
{
    /// <summary>
    /// Initializes a validation context without an in-memory assembly snapshot.
    /// </summary>
    /// <param name="assemblyPath">The path of the assembly candidate.</param>
    /// <param name="assemblyName">The identity of the assembly candidate.</param>
    public PluginAssemblyValidationContext(string assemblyPath, AssemblyName assemblyName)
        : this(assemblyPath, assemblyName, ReadOnlyMemory<byte>.Empty)
    {
    }

    /// <summary>
    /// Initializes a validation context with an in-memory assembly snapshot.
    /// </summary>
    /// <param name="assemblyPath">The path of the assembly candidate.</param>
    /// <param name="assemblyName">The identity of the assembly candidate.</param>
    /// <param name="assemblyBytes">The immutable content snapshot of the assembly candidate.</param>
    public PluginAssemblyValidationContext(
        string assemblyPath,
        AssemblyName assemblyName,
        ReadOnlyMemory<byte> assemblyBytes)
    {
        AssemblyPath = assemblyPath;
        AssemblyName = assemblyName;
        AssemblyBytes = assemblyBytes;
    }

    /// <summary>
    /// Gets the path of the assembly candidate. A validator may read the file directly instead of working
    /// on <see cref="AssemblyBytes"/>: where the hosting pipeline cannot keep the file stable for the
    /// duration of the call, it compares the file against the snapshot again before loading it, so a
    /// divergence leads to a rejected candidate rather than to an unnoticed load.
    /// </summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Gets the identity of the assembly candidate.
    /// </summary>
    public AssemblyName AssemblyName { get; }

    /// <summary>
    /// Gets the immutable candidate content snapshot used by the hosting pipeline.
    /// </summary>
    public ReadOnlyMemory<byte> AssemblyBytes { get; }
}
