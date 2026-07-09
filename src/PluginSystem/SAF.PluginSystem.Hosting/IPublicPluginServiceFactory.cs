// SPDX-FileCopyrightText: 2025-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting;

using Microsoft.Extensions.DependencyInjection;
using System;

/// <summary>
/// Defines a factory that resolves a public plugin service instance for a specific <see cref="ServiceDescriptor"/>.
/// </summary>
public interface IPublicPluginServiceFactory
{
    /// <summary>
    /// Gets the service descriptor handled by this factory.
    /// </summary>
    ServiceDescriptor ServiceDescriptor { get; }

    /// <summary>
    /// Resolves the service instance using the specified <paramref name="serviceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The plugin service provider used for resolution.</param>
    /// <returns>The resolved service instance, or <see langword="null"/>.</returns>
    object? Resolve(IServiceProvider serviceProvider);
}
