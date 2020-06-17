// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using SAF.Common;

namespace SAF.Messaging.Routing
{
    public class RoutingConfiguration
    {
        public MessagingConfiguration Messaging { get; set;  }
        public string[] PublishPatterns { get; set; }
        public string[] SubscriptionPatterns { get; set; }
    }
}