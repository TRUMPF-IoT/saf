// SPDX-FileCopyrightText: 2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestPlugin.PluginA;

using System.Reflection;
using TestPlugin.DependencyA;
using TestPlugin.PublicDependencyA;

public static class PluginAEntry
{
    public static string Name => nameof(PluginAEntry);
    public static Assembly GetDependencyAssembly() => typeof(DependencyAMarker).Assembly;
    public static Assembly GetPublicDependencyAssembly() => typeof(PublicDependencyAMarker).Assembly;
}