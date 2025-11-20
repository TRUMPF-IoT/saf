// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Diagnostics;

using System.Diagnostics;

internal class SafVersionInfo
{
    public SafVersionInfo()
    {
        var safType = typeof(SafVersionInfo);
        var assembly = safType.Assembly;
        BuildNumber = assembly.GetName().Version?.ToString() ?? string.Empty;
        if(!string.IsNullOrEmpty(assembly.Location))
        {
            Version = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion ?? string.Empty;
            BuildDate = File.GetLastWriteTimeUtc(assembly.Location);
        }
    }

    public string Version { get; set; } = string.Empty;
    public string BuildNumber { get; set; }
    public DateTimeOffset BuildDate { get; set; }
}