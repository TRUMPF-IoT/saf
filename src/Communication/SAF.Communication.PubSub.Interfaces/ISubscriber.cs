// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Communication.PubSub.Interfaces
{
    public interface ISubscriber
    {
        ISubscription Subscribe(params string[] patterns);
        ISubscription Subscribe(RoutingOptions routingOptions, params string[] patterns);

        void Unsubscribe(ISubscription subscription);
    }
}