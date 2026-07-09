// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace TestPlugin.PublicDependencyA;

public interface IPublicTransient
{
    object? GetPrivateSingleton();
    object? GetPrivateTransient();

    object? GetPrivateServiceOfOtherPlugin();
}
