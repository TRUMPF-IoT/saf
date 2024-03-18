// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// An interface for accessing SAF shared services like infrastructure services.
/// Shared services will be redirected to the SAF plug-in DI containers.
/// </summary>
public interface ISharedServiceRegistry
{
    /// <summary>
    /// Gets the IServiceCollection containing shared services that are registered with this instance.
    /// </summary>
    IServiceCollection Services { get; }
}