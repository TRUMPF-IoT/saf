// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde.Diagnostics;

public class CdePluginInfo
{
    public string Name { get; set; } = default!;
    public string Version { get; set; } = default!;
    public string BuildNumber { get; set; } = default!;
    public DateTimeOffset BuildDate { get; set; }
}