// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Threading;
using SAF.Common;

namespace SAF.DevToolbox.TestRunner
{
    public abstract class TestSequenceBase : IMessageHandler
    {
        private readonly IMessagingInfrastructure _messaging;
        private readonly Dictionary<string, Action<string>> _persistActions = new();

        internal Action<string> TraceTitleAction { get; set; }
        internal Action<string, string> TraceDocumentationAction { get; set; }
        
        public bool CanHandle(Message message) => true;

        protected TestSequenceBase(IMessagingInfrastructure messaging)
        {
            _messaging = messaging;
        }

        public void Handle(Message message)
        {
            if (_persistActions.ContainsKey(message.Topic))
            {
                _persistActions[message.Topic].Invoke(message.Payload);
            }
        }

        protected void WaitForValueSet(ref string value, int timeoutSeconds)
        {
            int c = 0;
            while (value == null && ++c < (timeoutSeconds * 5))
                Thread.Sleep(200);

            if (value == null)
                throw new TimeoutException();
        }

        protected void PayloadToVariable<T>(string topic, Action<string> persistAction) where T : IMessageHandler
        {
            _messaging.Subscribe<T>(topic);
            _persistActions[topic] = persistAction;
        }

        protected void PayloadToVariable(string topic, Action<string> persistAction)
        {
            _messaging.Subscribe(topic, msg => persistAction(msg.Payload));
        }

        protected void TraceDocumentation(string title, string doc) => TraceDocumentationAction?.Invoke(title, doc);

        protected void TraceTitle(string title) => TraceTitleAction?.Invoke(title);

        public abstract void Run();
    }
}
