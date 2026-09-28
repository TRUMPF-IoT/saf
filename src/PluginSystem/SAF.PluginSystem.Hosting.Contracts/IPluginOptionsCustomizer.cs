// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting.Contracts;

/// <summary>
/// Adjusts an options object a plug-in has built from configuration, before the plug-in uses it.
/// </summary>
/// <typeparam name="TOptions">The options type to customize, declared by the plug-in.</typeparam>
/// <remarks>
/// This is the seam for values a host cannot put into configuration: a secret it decrypts itself, a
/// constant compiled into the host, a path only the host knows. In 10.x a host configured such values
/// by calling the infrastructure's <c>Add*</c> extension directly; in 11.x the plug-in owns that call,
/// so the host contributes through a customizer instead.
///
/// Register implementations in the host container with
/// <c>AddPluginOptionsCustomizer</c> (<c>SAF.PluginSystem.Hosting</c>), which also forwards them into
/// every plugin container. A plug-in applies them with
/// <see cref="PluginOptionsCustomizerExtensions.ApplyPluginOptionsCustomizers{TOptions}"/> right after
/// binding its configuration.
///
/// Customizers run in registration order and after the plug-in's own binding, so for the properties a
/// customizer touches it always wins over the configured value.
/// </remarks>
public interface IPluginOptionsCustomizer<in TOptions>
    where TOptions : class
{
    /// <summary>
    /// Applies the host's adjustments to <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options instance the plug-in is about to use.</param>
    void Customize(TOptions options);
}
