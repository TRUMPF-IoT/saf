// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// An interface for accessing SAF shared services like infrastructure services.
/// Such services will be registered using IServiceHostBuilder.AddSharedSingleton.
/// </summary>
public interface ISharedServiceRegistry
{
    /// <summary>
    /// Gets the shared services registered with this instance.
    /// </summary>
    IEnumerable<ServiceDescriptor> Services { get; }
}