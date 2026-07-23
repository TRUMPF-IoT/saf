// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using SAF.PluginSystem.Hosting.AssemblyLoading;

using System.Reflection;

/// <summary>
/// Predicate-based <see cref="ISharedAssemblyResolver"/> for load-context integration tests. It reports
/// <see cref="SharedAssemblyDecision.ShareFromDefault"/> for assemblies matching the predicate and
/// <see cref="SharedAssemblyDecision.LoadIsolated"/> otherwise.
/// </summary>
internal sealed class TestSharedAssemblyResolver(Func<AssemblyName, bool> isShared) : ISharedAssemblyResolver
{
    /// <summary>
    /// Shares exactly the assemblies the host actually provides, i.e. those present in the application
    /// base directory (contracts, the shared Microsoft.Extensions.* abstractions, framework assemblies and
    /// the public test dependencies). Plugin-private dependencies live only in the plugin folder and stay
    /// isolated. This mirrors the closure the real <see cref="ISharedAssemblyRegistry"/> computes.
    /// </summary>
    public static TestSharedAssemblyResolver SharesHostProvidedAssemblies { get; } = new(name =>
        name.Name is not null &&
        File.Exists(Path.Combine(AppContext.BaseDirectory, name.Name + ".dll")));

    public SharedAssemblyDecision Resolve(AssemblyName requested, out Version? hostVersion)
    {
        hostVersion = null;
        return isShared(requested) ? SharedAssemblyDecision.ShareFromDefault : SharedAssemblyDecision.LoadIsolated;
    }
}
