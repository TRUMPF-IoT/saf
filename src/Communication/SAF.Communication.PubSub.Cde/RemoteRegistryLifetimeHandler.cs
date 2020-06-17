// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using nsCDEngine.BaseClasses;
using nsCDEngine.ViewModels;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde
{
    internal class RemoteRegistryLifetimeHandler : RegistryLifetimeHandlerBase<TSM>
    {
        public RemoteRegistryLifetimeHandler() : base(SubscriptionRegistry.AliveIntervalSeconds)
        {
        }

        public void HandleMessage(TheProcessMessage msg)
        {
            if(msg.Message.TXT.StartsWith(MessageToken.RegistryAlive) ||
                msg.Message.TXT.StartsWith(MessageToken.DiscoveryResponse) ||
                msg.Message.TXT.StartsWith(MessageToken.SubscribeTrigger))
            {
                var registryIdentity = TheCommonUtils.DeserializeJSONStringToObject<RegistryIdentity>(msg.Message.PLS);
                HandleMessage(msg.Message.ORG, registryIdentity.instanceId, msg.Message.TXT, msg.Message);
            }
            else if(msg.Message.TXT.StartsWith(MessageToken.SubscribeResponse))
            {
                var subscribeResponse = TheCommonUtils.DeserializeJSONStringToObject<RegistrySubscriptionResponse>(msg.Message.PLS);
                HandleMessage(msg.Message.ORG, subscribeResponse.instanceId, msg.Message.TXT, msg.Message);
            }
            else
            {
                HandleMessage(msg.Message.ORG, null, msg.Message.TXT, msg.Message);
            }
        }
    }
}
