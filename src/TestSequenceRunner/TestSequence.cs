// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using SAF.Common;
using SAF.Toolbox.Serialization;
using SAF.DevToolbox.TestRunner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestSequenceRunner
{
    internal class TestSequence : TestSequenceBase
    {
        private readonly ILogger _log;
        private readonly IMessagingInfrastructure _messaging;

        private readonly string _myServiceId = Guid.NewGuid().ToString().ToLowerInvariant();

        public TestSequence(ILogger<TestSequence> log, IMessagingInfrastructure messaging) : base(messaging)
        {
            _log = log as ILogger ?? NullLogger.Instance;
            _messaging = messaging;
        }

        public override void Run()
        {
            TraceTitle("Ping");
            TraceDocumentation(string.Empty, "Sending a ping and write something to get some documentation.");

            _log.LogInformation("Publish \"ping\" message.");

            var pingReturnTopic = $"ping/{_myServiceId}/response";
            var pingReturnValue = string.Empty;
            PayloadToVariable<TestSequence>(pingReturnTopic, payload => pingReturnValue = payload);

            _messaging.Publish(new Message
            {
                Topic = "ping/request",
                Payload = JsonSerializer.Serialize(new { ReplyTo = pingReturnTopic })
            });

            WaitForValueSet(ref pingReturnValue, 15);

            TraceDocumentation("Answer received", "Seems to be OK.");
            _log.LogInformation($"Got \"ping\" answer: {pingReturnValue}");

            _log.LogInformation("Test sequence successfully passed!");
        }
    }
}
