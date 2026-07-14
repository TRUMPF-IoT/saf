// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.PluginSystem.Hosting;

using Contracts;
using System.Reflection;

internal sealed class PluginManifestLoader : IPluginManifestLoader
{
    public IPluginManifest? LoadPluginManifest(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in GetLoadableTypes(assembly))
        {
            if (!type.IsClass || type.IsAbstract || !typeof(IPluginManifest).IsAssignableFrom(type))
            {
                continue;
            }

            return Activator.CreateInstance(type) as IPluginManifest;
        }

        return null;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
