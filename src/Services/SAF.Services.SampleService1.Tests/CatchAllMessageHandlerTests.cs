// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Common;
using SAF.Services.SampleService1.MessageHandlers;
using Xunit;

namespace SAF.Services.SampleService1.Tests
{
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
}