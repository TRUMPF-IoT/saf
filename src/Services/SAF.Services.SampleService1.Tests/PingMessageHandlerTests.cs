// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using NSubstitute;
using SAF.Common;
using SAF.Services.SampleService1.MessageHandlers;
using Xunit;

namespace SAF.Services.SampleService1.Tests;

public class PingMessageHandlerTests
{
    [Fact]
    public void RepliesMessagesToSender()
    {
        // Arrange
        var meshMock = Substitute.For<IMessagingInfrastructure>();
        var message = new Message { Topic = "ping/request", Payload = "{ \"replyTo\": \"ping/response\", \"id\": \"1\" }" };
        var sut = new PingMessageHandler(null, meshMock);

        // Act
        sut.Handle(message);

        // Assert
        meshMock.Received().Publish(Arg.Is<Message>(m => m.Topic == "ping/response" && m.Payload == "{\"id\":\"1\"}"));
    }
}