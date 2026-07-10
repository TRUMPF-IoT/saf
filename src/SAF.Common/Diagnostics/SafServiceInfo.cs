// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common.Diagnostics;

using System.Diagnostics;
using SAF.PluginSystem.Hosting.Contracts;

public class SafServiceInfo
{
    public SafServiceInfo(IPluginManifest manifest)
    {
        var type = manifest.GetType();
        var assembly = type.Assembly;

        Name = type.AssemblyQualifiedName ?? string.Empty;
        FriendlyName = type.Name;

        if (!string.IsNullOrEmpty(assembly.Location))
        {
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            Version = fvi.ProductVersion ?? string.Empty;
            BuildDate = File.GetLastWriteTimeUtc(assembly.Location);
        }

        BuildNumber = assembly.GetName().Version?.ToString() ?? string.Empty;
    }

    public string Name { get; set; } = string.Empty;

    public string FriendlyName { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string BuildNumber { get; set; } = string.Empty;

    public DateTimeOffset BuildDate { get; set; }
}
