// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Aggregates services registered across all plugin dependency injection (DI) containers
/// and exposes them through a unified resolution interface.
/// This is the sole mechanism for inter-plugin communication.
/// </summary>
/// <remarks>
/// Resolution queries every plugin's DI container and collects matching registrations.
/// For single-service methods (<see cref="GetService{T}"/> and <see cref="GetKeyedService{T}"/>),
/// exactly one registration across all containers is expected; multiple matches will throw.
/// Standard .NET DI lifetime semantics apply within each container: a singleton registration
/// returns the same instance the owning plugin uses internally, while a transient registration
/// creates a new instance on each call.
/// </remarks>
public interface IPluginServiceProvider
{
    /// <summary>
    /// Resolves a service of type <typeparamref name="T"/> from the aggregated plugin containers.
    /// </summary>
    /// <typeparam name="T">The service type to resolve.</typeparam>
    /// <returns>The resolved service instance, or <see langword="null"/> if no service of that type is registered.</returns>
    T? GetService<T>();

    /// <summary>
    /// Resolves a keyed service of type <typeparamref name="T"/> from the aggregated plugin containers.
    /// </summary>
    /// <typeparam name="T">The service type to resolve.</typeparam>
    /// <param name="key">The string key identifying the specific service registration.</param>
    /// <returns>The resolved service instance, or <see langword="null"/> if no matching keyed service is registered.</returns>
    T? GetKeyedService<T>(string key);

    /// <summary>
    /// Resolves all services of type <typeparamref name="T"/> from all plugin containers.
    /// </summary>
    /// <typeparam name="T">The service type to resolve.</typeparam>
    /// <returns>An enumerable of all registered service instances of the specified type.</returns>
    IEnumerable<T> GetServices<T>();

    /// <summary>
    /// Resolves all keyed services of type <typeparamref name="T"/> matching the specified key
    /// from all plugin containers.
    /// </summary>
    /// <typeparam name="T">The service type to resolve.</typeparam>
    /// <param name="key">The string key identifying the specific service registrations.</param>
    /// <returns>An enumerable of all matching keyed service instances.</returns>
    IEnumerable<T> GetKeyedServices<T>(string key);
}