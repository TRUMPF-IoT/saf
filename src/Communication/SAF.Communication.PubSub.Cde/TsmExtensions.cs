// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde
{
    internal static class TsmExtensions
    {
        public static bool IsRoutingAllowed(this TSM tsm, RoutingOptions routingOptions)
        {
            switch (routingOptions)
            {
                case RoutingOptions.All:
                    return true;

                case RoutingOptions.Local:
                    return tsm.IsLocalHost();

                case RoutingOptions.Remote:
                    return !tsm.IsLocalHost();

                default:
                    throw new ArgumentOutOfRangeException(nameof(routingOptions));
            }
        }
    }
}