// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using SAF.Communication.PubSub.Cde.Authorization;
using SAF.Communication.PubSub.Cde.MessageHandler.Authorization;

namespace SAF.Communication.PubSub.Cde.MessageHandler
{
    /// <summary>
    /// Wird nur für pattern (bzw. Topic) "internal/auth" verwendet?
    /// </summary>
    internal class MessageListener : IDisposable
    {
        private readonly AuthorizationService _authService;
        private readonly ISubscriptionInternal _subscription;

        public MessageListener(Subscriber subscriber, Publisher publisher)
        {
            _authService = new AuthorizationService(publisher);
            _subscription = Init(subscriber);            
        }

        private ISubscriptionInternal Init(Subscriber subscriber)
        {
            var messageHandler =
                new CheckTokenHandler(_authService,
                    new GetTokenHandler(_authService, null));

            var subscription = subscriber.Subscribe(new[]
            {
                $"{AuthorizationService.BaseChannelName}/*"
            }) as ISubscriptionInternal;

            subscription?.SetRawHandler((msgVersion, msg) =>
            {
                messageHandler.Handle(msgVersion, msg);
            });
            return subscription;
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
