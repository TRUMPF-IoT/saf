// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde;
using Microsoft.Extensions.Configuration;
using SAF.PluginSystem.Hosting.Contracts;

public static class PluginSystemHostContextExtensions
{
    private const string CdeSectionName = "Cde";

    /// <summary>
    /// Returns the <c>Cde</c> section of the plug-in configuration, or the one of the host configuration if
    /// the plug-in configuration has none.
    /// </summary>
    /// <param name="context">The plug-in's host context.</param>
    /// <returns>The <c>Cde</c> section every C-DEngine plug-in reads.</returns>
    public static IConfigurationSection GetCdeConfigurationSection(this IPluginSystemHostContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var section = context.PluginConfiguration.GetSection(CdeSectionName);
        return section.Exists() ? section : context.HostConfiguration.GetSection(CdeSectionName);
    }
}