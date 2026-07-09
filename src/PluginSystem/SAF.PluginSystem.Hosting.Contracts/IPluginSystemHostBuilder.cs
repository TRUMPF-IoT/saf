// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting.Contracts;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides the builder API for configuring the plugin system host, including
/// environment settings, configuration sources, and host-level service registrations.
/// </summary>
public interface IPluginSystemHostBuilder
{
    /// <summary>
    /// Gets the <see cref="IPluginSystemHostEnvironment"/> for the host being built.
    /// </summary>
    IPluginSystemHostEnvironment Environment { get; }

    /// <summary>
    /// Gets the <see cref="IConfigurationManager"/> for adding configuration sources to the host.
    /// </summary>
    IConfigurationManager Configuration { get; }

    /// <summary>
    /// Gets the <see cref="IServiceCollection"/> for registering host-level services.
    /// </summary>
    IServiceCollection Services { get; }
}