// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde;
using nsCDEngine.ViewModels;
using Interfaces;

internal interface ISubscriptionInternal : ISubscription
{
    void SetRawHandler(Action<string, TheProcessMessage> callback);
}