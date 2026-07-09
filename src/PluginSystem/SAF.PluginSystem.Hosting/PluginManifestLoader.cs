// SPDX-FileCopyrightText: 2024 TRUMPF Laser SE
//
// SPDX-License-Identifier: LicenseRef-TRUMPF

namespace SAF.PluginSystem.Hosting;

using Contracts;
using System.Reflection;

internal sealed class PluginManifestLoader : IPluginManifestLoader
{
    public IPluginManifest? LoadPluginManifest(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in assembly.GetTypes())
        {
            if (!typeof(IPluginManifest).IsAssignableFrom(type))
            {
                continue;
            }

            return Activator.CreateInstance(type) as IPluginManifest;
        }

        return null;
    }
}