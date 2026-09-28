// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using AssemblyLoading;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registers host adjustments for options that a plug-in binds from configuration.
/// </summary>
public static class PluginOptionsCustomizerForwarderExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TCustomizer"/> in the host container and forwards every
    /// <see cref="IPluginOptionsCustomizer{TOptions}"/> registered there into each plugin container.
    /// </summary>
    /// <typeparam name="TOptions">The plug-in options type to customize.</typeparam>
    /// <typeparam name="TCustomizer">The customizer implementation; resolved from the host container, so it
    /// can inject host services.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Call this once per customizer. Several customizers for the same <typeparamref name="TOptions"/> all
    /// reach the plug-in and run in registration order; the forwarder itself is registered only once.
    ///
    /// Like <see cref="HostServiceForwarderExtensions.AddHostServiceForwarder{T}(IServiceCollection)"/>
    /// this also shares the assemblies behind the forwarded type - here both the one declaring
    /// <see cref="IPluginOptionsCustomizer{TOptions}"/> and the one declaring
    /// <typeparamref name="TOptions"/>, which is the plug-in's own assembly. That is what lets the host
    /// name the plug-in's options type and the plug-in resolve the host's customizer as the same type.
    /// </remarks>
    public static IServiceCollection AddPluginOptionsCustomizer<TOptions, TCustomizer>(this IServiceCollection services)
        where TOptions : class
        where TCustomizer : class, IPluginOptionsCustomizer<TOptions>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPluginOptionsCustomizer<TOptions>, TCustomizer>();

        return services.AddPluginOptionsCustomizerForwarder<TOptions>();
    }

    /// <summary>
    /// Registers <paramref name="customize"/> as an <see cref="IPluginOptionsCustomizer{TOptions}"/> and
    /// forwards it into each plugin container.
    /// </summary>
    /// <typeparam name="TOptions">The plug-in options type to customize.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="customize">The adjustment to apply.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// The delegate is invoked inside the plug-in, once per options instance. It closes over whatever the
    /// host captured, so use the
    /// <see cref="AddPluginOptionsCustomizer{TOptions, TCustomizer}(IServiceCollection)"/> overload when the
    /// adjustment needs host services that are only resolvable later.
    /// </remarks>
    public static IServiceCollection AddPluginOptionsCustomizer<TOptions>(this IServiceCollection services, Action<TOptions> customize)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(customize);

        services.AddSingleton<IPluginOptionsCustomizer<TOptions>>(new DelegatePluginOptionsCustomizer<TOptions>(customize));

        return services.AddPluginOptionsCustomizerForwarder<TOptions>();
    }

    private static IServiceCollection AddPluginOptionsCustomizerForwarder<TOptions>(this IServiceCollection services)
        where TOptions : class
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostServiceForwarder, PluginOptionsCustomizerForwarder<TOptions>>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISharedAssemblySource, SharedAssemblySource<IPluginOptionsCustomizer<TOptions>>>());

        return services;
    }

    private sealed class DelegatePluginOptionsCustomizer<TOptions>(Action<TOptions> customize) : IPluginOptionsCustomizer<TOptions>
        where TOptions : class
    {
        public void Customize(TOptions options) => customize(options);
    }
}
