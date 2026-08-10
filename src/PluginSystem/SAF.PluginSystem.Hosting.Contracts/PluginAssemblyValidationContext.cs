// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using System.Reflection;

/// <summary>
/// Provides metadata and a stable content snapshot for validating a plugin assembly candidate before loading it.
/// </summary>
public sealed class PluginAssemblyValidationContext
{
    public PluginAssemblyValidationContext(string assemblyPath, AssemblyName assemblyName)
        : this(assemblyPath, assemblyName, ReadOnlyMemory<byte>.Empty)
    {
    }

    public PluginAssemblyValidationContext(
        string assemblyPath,
        AssemblyName assemblyName,
        ReadOnlyMemory<byte> assemblyBytes)
    {
        AssemblyPath = assemblyPath;
        AssemblyName = assemblyName;
        AssemblyBytes = assemblyBytes;
    }

    public string AssemblyPath { get; }

    public AssemblyName AssemblyName { get; }

    /// <summary>
    /// Gets the immutable candidate content snapshot used by the hosting pipeline.
    /// </summary>
    public ReadOnlyMemory<byte> AssemblyBytes { get; }
}
