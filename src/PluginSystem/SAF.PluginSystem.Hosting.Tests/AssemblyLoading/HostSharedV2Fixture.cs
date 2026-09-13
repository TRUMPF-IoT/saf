// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Tests.AssemblyLoading;

using System.Reflection;
using System.Runtime.Loader;

// Loads the host's V2 of TestPlugin.SharedVersioned into the process-global default context once for the
// class. This is a deliberate, permanent side effect: the "share from default" path resolves from
// AssemblyLoadContext.Default, which cannot be unloaded, so the host version has to live there. Centralized
// here so it is loaded exactly once and any test asserting on default-context resolution of
// TestPlugin.SharedVersioned accounts for it.
public sealed class HostSharedV2Fixture
{
    public Assembly Assembly { get; } = AssemblyLoadContext.Default.LoadFromAssemblyPath(
        Path.Combine(AppContext.BaseDirectory, "shared-v2", "TestPlugin.SharedVersioned.dll"));
}
