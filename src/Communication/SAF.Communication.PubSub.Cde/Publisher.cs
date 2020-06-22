// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System;
using System.Threading;
using System.Threading.Tasks;
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
            Publish(channel, message, Guid.Empty);
        }

        public void Publish(string channel, string message, RoutingOptions routingOptions)
        {
            Publish(channel, message, Guid.Empty, routingOptions);
        }

        public void Publish(string channel, string message, Guid userId)
        {
            Publish(channel, message, $"{userId}");
        }

        public void Publish(string channel, string message, Guid userId, RoutingOptions routingOptions)
        {
            Publish(channel, message, $"{userId}", routingOptions);
        }

        public void Publish(string channel, string message, string userId)
        {
            Publish(channel, message, userId, RoutingOptions.All);
        }

        public void Publish(string channel, string message, string userId, RoutingOptions routingOptions)
        {
            var t = new Topic { Channel = channel, MsgId = Guid.NewGuid().ToString("N") };
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