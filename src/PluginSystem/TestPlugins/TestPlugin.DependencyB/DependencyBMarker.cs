// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestPlugin.DependencyB;

using System.Reflection;
using TestPlugin.DependencyA;
using TestPlugin.PublicDependencyA;

public static class DependencyBMarker
{
    public static string Name => nameof(DependencyBMarker);
    public static Assembly GetDependencyAssembly() => typeof(DependencyAMarker).Assembly;
    public static Assembly GetPublicDependencyAssembly() => typeof(PublicDependencyAMarker).Assembly;
}