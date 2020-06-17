// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Linq;
using SAF.Common;

namespace SAF.Messaging.Routing
{
    internal class MessageRoutingSubscription : IDisposable
    {
        private readonly IMessagingInfrastructure _messaging;
        private object[] _subscriptionHandles;

        public MessageRoutingSubscription(IMessagingInfrastructure messaging)
        {
            _messaging = messaging;
        }

        public void Subscribe(string[] patterns, Action<Message> handler)
        {
            _subscriptionHandles = patterns.Select(p => _messaging.Subscribe(p, handler)).ToArray();
        }

        public void Subscribe<TMessageHandler>(string[] patterns) where TMessageHandler : IMessageHandler
        {
            _subscriptionHandles = patterns.Select(p => _messaging.Subscribe<TMessageHandler>(p)).ToArray();
        }

        public void Dispose()
        {
            if (_subscriptionHandles == null) return;
            Array.ForEach(_subscriptionHandles, handle => _messaging.Unsubscribe(handle));
            _subscriptionHandles = null;
        }
    }
}