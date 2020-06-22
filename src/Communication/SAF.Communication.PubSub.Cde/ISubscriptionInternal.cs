// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using nsCDEngine.ViewModels;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub.Cde
{
    internal interface ISubscriptionInternal : ISubscription
    {
        void With(Action<TheProcessMessage> callback);
    }
}