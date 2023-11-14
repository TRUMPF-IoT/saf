// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Communication.PubSub.Cde.Authorization;
using SAF.Communication.PubSub.Cde.MessageHandler.Authorization;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde.MessageHandler;

internal class MessageListener : IDisposable
{
    private readonly AuthorizationService _authService;
    private readonly ISubscriptionInternal _subscription;

    public MessageListener(Subscriber subscriber, IPublisher publisher)
    {
        _authService = new AuthorizationService(publisher);
        _subscription = Init(subscriber);            
    }

    private ISubscriptionInternal Init(Subscriber subscriber)
    {
        var messageHandler =
            new CheckTokenHandler(_authService,
                new GetTokenHandler(_authService, null));

        var subscription = (ISubscriptionInternal)subscriber.Subscribe($"{AuthorizationService.BaseChannelName}/*");
        subscription.SetRawHandler(messageHandler.Handle);

        return subscription;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _subscription?.Dispose();
    }
}