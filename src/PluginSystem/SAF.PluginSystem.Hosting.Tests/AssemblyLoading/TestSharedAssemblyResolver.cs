// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using SAF.PluginSystem.Hosting.AssemblyLoading;

using System.Reflection;

/// <summary>
/// Configurable <see cref="ISharedAssemblyResolver"/> stub for load-context integration tests.
/// </summary>
internal sealed class TestSharedAssemblyResolver(Func<AssemblyName, (SharedAssemblyDecision Decision, Version? HostVersion)> resolve)
    : ISharedAssemblyResolver
{
    /// <summary>
    /// Shares exactly the assemblies the host actually provides, i.e. those present in the application
    /// base directory (contracts, the shared Microsoft.Extensions.* abstractions, framework assemblies and
    /// the public test dependencies). Plugin-private dependencies live only in the plugin folder and stay
    /// isolated. This mirrors the closure the real <see cref="ISharedAssemblyRegistry"/> computes.
    /// </summary>
    public static TestSharedAssemblyResolver SharesHostProvidedAssemblies { get; } = new(requested =>
        requested.Name is not null && File.Exists(Path.Combine(AppContext.BaseDirectory, requested.Name + ".dll"))
            ? (SharedAssemblyDecision.ShareFromDefault, null)
            : (SharedAssemblyDecision.LoadIsolated, null));

    /// <summary>
    /// Returns <paramref name="decision"/>/<paramref name="hostVersion"/> for the assembly named
    /// <paramref name="simpleName"/> and isolates everything else.
    /// </summary>
    public static TestSharedAssemblyResolver WithFixedDecision(string simpleName, SharedAssemblyDecision decision, Version? hostVersion)
        => new(requested => string.Equals(requested.Name, simpleName, StringComparison.OrdinalIgnoreCase)
            ? (decision, hostVersion)
            : (SharedAssemblyDecision.LoadIsolated, null));

    public SharedAssemblyDecision Resolve(AssemblyName requested, out Version? hostVersion)
    {
        (var decision, hostVersion) = resolve(requested);
        return decision;
    }
}
