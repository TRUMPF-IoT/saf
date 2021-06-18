// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System;
using System.Threading;
using System.Threading.Tasks;
using SAF.Common;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde
{
    public class Publisher : IPublisher, IDisposable
    {
        private bool _disposed;
        private readonly ComLine _line;
        private readonly CancellationTokenSource _tokenSource;
        private SubscriptionRegistry _subscriptionRegistry;

        public Publisher(ComLine line)
            : this(line, new CancellationTokenSource())
        { }

        public Publisher(ComLine line, CancellationToken token)
            : this(line, CancellationTokenSource.CreateLinkedTokenSource(token))
        { }

        public Publisher(ComLine line, CancellationTokenSource tokenSource)
        {
            _line = line;
            _tokenSource = tokenSource;
        }

        public async Task<IPublisher> ConnectAsync()
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_tokenSource.Token);
            try
            {
                _subscriptionRegistry = new SubscriptionRegistry(_line);
                await _subscriptionRegistry.ConnectAsync(linkedCts.Token);
                return this;
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        public void Publish(string channel, string message)
        {
            Publish(new Message { Topic = channel, Payload = message});
        }

        public void Publish(string channel, string message, RoutingOptions routingOptions)
        {
            Publish(new Message { Topic = channel, Payload = message }, Guid.Empty, routingOptions);
        }

        public void Publish(Message message)
        {
            Publish(message, Guid.Empty);
        }

        public void Publish(Message message, RoutingOptions routingOptions)
        {
            Publish(message, Guid.Empty, routingOptions);
        }

        public void Publish(Message message, Guid userId)
        {
            Publish(message, $"{userId}");
        }

        public void Publish(Message message, Guid userId, RoutingOptions routingOptions)
        {
            Publish(message, $"{userId}", routingOptions);
        }

        public void Publish(Message message, string userId)
        {
            Publish(message, userId, RoutingOptions.All);
        }

        public void Publish(Message message, string userId, RoutingOptions routingOptions)
        {
            var t = new Topic { Channel = message.Topic, MsgId = Guid.NewGuid().ToString("N") };
            _subscriptionRegistry.Broadcast(t, message, userId, routingOptions);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _tokenSource.Cancel();
            _tokenSource.Dispose();

            _subscriptionRegistry?.Dispose();
        }
    }
}