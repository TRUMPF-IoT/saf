// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Routing;
using Common;

public class RoutingConfiguration
{
    public MessagingConfiguration Messaging { get; set; } = default!;
    public string[]? PublishPatterns { get; set; }
    public string[]? SubscriptionPatterns { get; set; }
}