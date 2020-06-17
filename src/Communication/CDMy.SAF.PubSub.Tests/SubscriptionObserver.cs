// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using SAF.Communication.PubSub.Interfaces;

namespace CDMy.SAF.PubSub.Tests
{
    public static class SubscriptionObserver
    {
        public static IObservable<MessagePacket> Observe(ISubscription subscription)
        {
            return Observable.Create<MessagePacket>(observer =>
            {
                var disposable = Disposable.Create(subscription.Unsubscribe);
                subscription.With((time, channel, message) => observer.OnNext(new MessagePacket(time, channel, message)));
                return disposable;
            });
        }
    }
}

public struct MessagePacket
{
    public DateTimeOffset Time { get; set; }
    public string Channel { get; set; }
    public string Message { get; set; }

    public MessagePacket(DateTimeOffset time, string channel, string message) : this()
    {
        Time = time;
        Channel = channel;
        Message = message;
    }
}