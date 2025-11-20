// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Services.SampleService1.MessageHandlers;
using Microsoft.Extensions.Logging;
using Common;

public class CatchAllMessageHandler : IMessageHandler
{
    private readonly ILogger<CatchAllMessageHandler> _log;

    public CatchAllMessageHandler(ILogger<CatchAllMessageHandler> log)
    {
        _log = log;
    }

    public bool CanHandle(Message message)
        => true;

    public void Handle(Message message)
    {
        _log.LogInformation($"Message: {message.Topic}, Payload: {message.Payload}");
    }
}