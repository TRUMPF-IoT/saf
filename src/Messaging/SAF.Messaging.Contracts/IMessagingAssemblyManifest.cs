// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common;

public class MessagingConfiguration
{
    public string Type { get; set; } = string.Empty;
    public IDictionary<string, string>? Config { get; set; }
}
