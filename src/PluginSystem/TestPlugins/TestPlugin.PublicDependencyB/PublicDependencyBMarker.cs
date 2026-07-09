// SPDX-FileCopyrightText: 2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace TestPlugin.PublicDependencyB;

using System.Reflection;
using TestPlugin.PublicDependencyA;

public static class PublicDependencyBMarker
{
    public static string Name => nameof(PublicDependencyBMarker);
    public static Assembly GetPublicDependencyAssembly() => typeof(PublicDependencyAMarker).Assembly;
}