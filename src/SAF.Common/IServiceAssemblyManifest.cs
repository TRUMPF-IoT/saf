// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.DependencyInjection;

namespace SAF.Common;

/// <summary>
///     Describes a service assembly and provides a method where the services are registered to the hosted DI container.
///     Should appear exactly once, within a service assembly.
/// </summary>
public interface IServiceAssemblyManifest
{
    /// <summary>
    ///     Gets a friendly name for this assembly (usually for logging purposes).
    /// </summary>
    string FriendlyName { get; }

    /// <summary>
    ///     Register the dependencies (services and their dependencies) to a prepared, hosted DI container.
    ///     Use Microsoft.Extensions.DependencyInjection.Abstractions extension methods to register (AddTransient, ...).
    /// </summary>
    /// <param name="services">The collection to register the services.</param>
    /// <param name="context">Information about the SAF hosting context the services will run in.</param>
    void RegisterDependencies(IServiceCollection services, IServiceHostContext context);
}