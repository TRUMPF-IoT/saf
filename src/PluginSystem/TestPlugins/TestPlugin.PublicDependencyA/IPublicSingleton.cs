// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace TestPlugin.PublicDependencyA;

public interface IPublicSingleton
{
    object? GetPrivateSingleton();
    Type GetPrivateSingletonType();
    object? GetPrivateTransient();
    Type GetPrivateTransientType();

    object? GetPrivateServiceOfOtherPlugin();
}
