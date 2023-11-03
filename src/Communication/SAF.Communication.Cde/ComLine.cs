// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;

namespace SAF.Communication.Cde
{
    public delegate void MessageReceivedHandler(ICDEThing sender, object msg);

    /// <summary>
    /// <para>Abstract link class between the C-DEngine object <c>TheThing</c> and the SAF objects
    /// <c>Subscriber</c> and <c>SubscriptionRegistry</c>.</para>For the concret implementations see
    /// <see cref="ConnectionTypes.DefaultComLine">DefaultComLine</see> and 
    /// <see cref="ConnectionTypes.AdvancedComLine">AdvancedComLine</see>
    /// </summary>
    public abstract class ComLine
    {
        public abstract string Address { get; }
        public abstract event MessageReceivedHandler? MessageReceived;
        public abstract Task Subscribe(string topic);
        public abstract void Broadcast(TSM message);
        public abstract void AnswerToSender(TSM originalMessage, TSM reply);
    }
}