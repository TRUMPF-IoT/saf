// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestPlugin.SharedVersioned;

using System.Reflection;

/// <summary>
/// Stand-in for a shared (contract) dependency that exists in multiple versions. The reported major
/// version is read from the assembly itself, so the identical source compiled into the V1 and V2
/// assemblies reports a different value at runtime. This makes it observable which version a plugin
/// actually bound to.
/// </summary>
public static class SharedComponent
{
    public static int GetMajorVersion() => typeof(SharedComponent).Assembly.GetName().Version?.Major ?? 0;
}
