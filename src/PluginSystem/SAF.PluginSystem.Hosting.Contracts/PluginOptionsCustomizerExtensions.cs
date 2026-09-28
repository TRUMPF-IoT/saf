// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Lets a plug-in apply the <see cref="IPluginOptionsCustomizer{TOptions}"/> instances its host forwarded.
/// </summary>
public static class PluginOptionsCustomizerExtensions
{
    /// <summary>
    /// Runs every <see cref="IPluginOptionsCustomizer{TOptions}"/> registered in
    /// <paramref name="pluginServices"/> against <paramref name="options"/>, in registration order.
    /// </summary>
    /// <typeparam name="TOptions">The options type being customized.</typeparam>
    /// <param name="pluginServices">The plug-in's own <see cref="IServiceProvider"/>.</param>
    /// <param name="options">The freshly bound options instance.</param>
    /// <returns>The same <paramref name="options"/> instance, for chaining out of a factory delegate.</returns>
    /// <remarks>
    /// Call this from the factory that creates the options, not from the manifest's
    /// <c>ConfigureServices</c>: the forwarded customizers are registrations in the plug-in's service
    /// collection and can only be resolved once that container is built.
    ///
    /// With no customizer registered this returns <paramref name="options"/> untouched, so a plug-in can
    /// offer the seam unconditionally.
    /// </remarks>
    public static TOptions ApplyPluginOptionsCustomizers<TOptions>(this IServiceProvider pluginServices, TOptions options)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(pluginServices);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var customizer in pluginServices.GetServices<IPluginOptionsCustomizer<TOptions>>())
        {
            customizer.Customize(options);
        }

        return options;
    }
}
