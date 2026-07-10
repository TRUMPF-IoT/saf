// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Services.SampleService1.Tests;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Common;
using SAF.Messaging.Contracts;
using MessageHandlers;
using Xunit;

public class CatchAllMessageHandlerTests
{
    [Fact]
    public void LogsEverything()
    {
        // Arrange
        var loggerMock = Substitute.For<ILogger<CatchAllMessageHandler>>();
        var sut = new CatchAllMessageHandler(loggerMock);

        // Act
        sut.Handle(new Message { Topic = "Test topic", Payload = "{ }" });

        // Assert
        loggerMock.ReceivedWithAnyArgs().LogInformation("Message: Test topic"); // TODO: how is string comparison done with NSubstitute?
    }
}


