// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace TestPlugin.PublicDependencyA;

public interface IPublicSingleton
{
    object? GetPrivateSingleton();
    Type GetPrivateSingletonType();
    object? GetPrivateTransient();
    Type GetPrivateTransientType();

    object? GetPrivateServiceOfOtherPlugin();
}
