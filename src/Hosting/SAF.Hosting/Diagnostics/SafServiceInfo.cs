// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Diagnostics;

using System.Diagnostics;
using Contracts;

internal class SafServiceInfo
{
    public SafServiceInfo(IServiceAssemblyManifest assembly)
    {
        var type = assembly.GetType();

        var fvi = FileVersionInfo.GetVersionInfo(type.Assembly.Location);

        Name = type.AssemblyQualifiedName ?? string.Empty;
        FriendlyName = assembly.FriendlyName;
        Version = fvi.ProductVersion ?? string.Empty;
        BuildNumber = type.Assembly.GetName().Version?.ToString() ?? string.Empty;
        BuildDate = File.GetLastWriteTimeUtc(type.Assembly.Location);
    }

    public string Name { get; set; }
    public string FriendlyName { get; set; }
    public string Version { get; set; }
    public string BuildNumber { get; set; }
    public DateTimeOffset BuildDate { get; set; }
}