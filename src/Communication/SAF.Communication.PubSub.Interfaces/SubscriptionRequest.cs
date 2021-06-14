// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Interfaces
{
    public class SubscriptionRequest
    {
#pragma warning disable IDE1006 // naming convention
#pragma warning disable 0649    // ignore as it will be set by JSON deserialization
        public string id;
        public string[] topics;
        public string version;
#pragma warning restore 0649
#pragma warning restore IDE1006
    }
}
