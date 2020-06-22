// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using nsCDEngine.BaseClasses;
using nsCDEngine.Communication;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;

namespace SAF.Communication.Cde.ConnectionTypes
{
    public class AdvancedComLine : ComLine
    {
        private readonly TheThing _thing;
        private readonly TheISBConnect _line;
        private readonly List<string> _subscribedEngines = new List<string>();
        
        public bool IsConnected { get; private set; }

        public AdvancedComLine(TheThing thing, string address, string scope)
        {
            _thing = thing;

            var line = new TheISBConnect();
            line.RegisterEvent2("Connected", OnRouteConnected);
            line.RegisterEvent2("TSMReceived", OnRouteTsmReceived);
            line.RegisterEvent2("Disconnected", OnRouteDisconnected);
            line.Connect(address, scope);
            
            _line = line;
        }

        public override string Address => $"{_thing.cdeN}:{_thing.cdeMID}";

        public override event MessageReceivedHandler MessageReceived;

        public override Task Subscribe(string topic)
        {
            if(_subscribedEngines.Contains(topic)) return Task.FromResult(false);

            _subscribedEngines.Add(topic);
            _line.Subscribe(topic);

            return Task.FromResult(true); // TODO: as soon as no .Net FW v4.5 must be supported anymore, replace by Task.CompletedTask, which is cached
        }

        public override void Broadcast(TSM message)
        {
            message.SetOriginatorThing(_thing);
            _line.SendTSM(message);
        }

        public override void AnswerToSender(TSM originalMessage, TSM reply)
        {
            reply.SetOriginatorThing(_thing);
            _line.SendToOriginator(originalMessage, reply, originalMessage.IsLocalHost());
        }

        private void OnRouteConnected(TheProcessMessage message, object sender)
        {
            IsConnected = true;
            var topics = _thing.GetBaseThing().GetBaseEngine().GetEngineName();
            if(_subscribedEngines.Any()) topics += $";{string.Join(";", _subscribedEngines)}";
            _line.Subscribe(topics);
        }

        private void OnRouteDisconnected(TheProcessMessage message, object sender)
        {
            IsConnected = false;
        }

        private void OnRouteTsmReceived(TheProcessMessage message, object sender)
        {
            MessageReceived?.Invoke(_thing, message.Message);
        }
    }
}