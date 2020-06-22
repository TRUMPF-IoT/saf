// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Threading.Tasks;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;

namespace SAF.Communication.Cde
{
    public delegate void MessageReceivedHandler(ICDEThing sender, object msg);

    public abstract class ComLine
    {
        public abstract string Address { get; }
        public abstract event MessageReceivedHandler MessageReceived;
        public abstract Task Subscribe(string topic);
        public abstract void Broadcast(TSM message);
        public abstract void AnswerToSender(TSM originalMessage, TSM reply);
    }
}