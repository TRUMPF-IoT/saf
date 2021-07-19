// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;
using SAF.Toolbox.Serialization;

namespace SAF.Services.SampleService1.MessageHandlers
{
    internal class PingRequest
    {
        public string ReplyTo { get; set; }
        public string Id { get; set; }
    }

    public class PingMessageHandler : IMessageHandler
    {
        private readonly ILogger<PingMessageHandler> _log;
        private readonly IMessagingInfrastructure _messaging;

        public PingMessageHandler(ILogger<PingMessageHandler> log, IMessagingInfrastructure messaging)
        {
            _log = log ?? NullLogger<PingMessageHandler>.Instance;
            _messaging = messaging;
        }

        public bool CanHandle(Message message)
            => true;

        public void Handle(Message message)
        {
            var req = JsonSerializer.Deserialize<PingRequest>(message.Payload);
            var replyTo = req.ReplyTo;
            _log.LogInformation($"Message ping/request ({req.Id})" + (replyTo == null ? "" : $", answering with {replyTo}"));
            if (replyTo != null)
            {
                _messaging.Publish(new Message { Topic = replyTo, Payload = JsonSerializer.Serialize(new { req.Id }) });
            }
        }
    }
}