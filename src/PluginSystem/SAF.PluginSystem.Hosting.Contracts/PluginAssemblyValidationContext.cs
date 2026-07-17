// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using System.Reflection;

/// <summary>
/// Provides metadata for validating a plugin assembly candidate before loading it.
/// </summary>
public sealed class PluginAssemblyValidationContext(
    string assemblyPath,
    AssemblyName assemblyName)
{
    public string AssemblyPath { get; } = assemblyPath;

    public AssemblyName AssemblyName { get; } = assemblyName;
}
