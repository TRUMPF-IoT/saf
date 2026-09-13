// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestPlugin.PluginVersioned;

using System.Reflection;
using TestPlugin.SharedVersioned;

/// <summary>
/// Entry point invoked by the versioning integration tests. The plugin is compiled against
/// <c>TestPlugin.SharedVersioned</c> V1 and ships that version in its own folder.
/// </summary>
public static class PluginVersionedEntry
{
    public static int GetSharedMajorVersion() => SharedComponent.GetMajorVersion();

    public static Assembly GetSharedAssembly() => typeof(SharedComponent).Assembly;
}
