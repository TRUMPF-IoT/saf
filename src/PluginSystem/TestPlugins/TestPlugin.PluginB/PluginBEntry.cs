// SPDX-FileCopyrightText: 2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestPlugin.PluginB;

using System.Reflection;
using TestPlugin.DependencyB;
using TestPlugin.PublicDependencyB;

public static class PluginBEntry
{
    public static string Name => nameof(PluginBEntry);
    public static Assembly GetDependencyAssembly() => typeof(DependencyBMarker).Assembly;
    public static Assembly GetTransitiveDependencyAssembly() => DependencyBMarker.GetDependencyAssembly();
    public static Assembly GetPublicDependencyAssembly() => typeof(PublicDependencyBMarker).Assembly;
    public static Assembly GetTransitivePublicDependencyAssembly() => PublicDependencyBMarker.GetPublicDependencyAssembly();
}