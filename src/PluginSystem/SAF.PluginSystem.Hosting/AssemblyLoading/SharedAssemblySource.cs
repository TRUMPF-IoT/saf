// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.AssemblyLoading;

using System.Reflection;

/// <summary>
/// Shares the assembly that declares <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type whose declaring assembly must be shared.</typeparam>
public sealed class SharedAssemblySource<T> : ISharedAssemblySource
{
    /// <inheritdoc />
    public IEnumerable<AssemblyName> GetSharedAssemblyNames() => [typeof(T).Assembly.GetName()];
}
