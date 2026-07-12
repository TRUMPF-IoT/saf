// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Routing;

public class Configuration
{
    public RoutingConfiguration[] Routings { get; set; } = Array.Empty<RoutingConfiguration>();
}