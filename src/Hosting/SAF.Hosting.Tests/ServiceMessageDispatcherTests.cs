// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic;
using NSubstitute;
using Xunit;

public class ServiceMessageDispatcherTests
{
    [Fact]
    public void DispatchCallsHandleWhenItCanHandleMessage()
    {
        // Arrange
        var handler = Substitute.For<DummyMessageHandler>();
        handler.CanHandle(Arg.Any<Message>()).Returns(true);

        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance);
        var testMessage = new Message();

        // Act
        dispatcher.AddHandler<DummyMessageHandler>(() => handler);
        dispatcher.DispatchMessage<DummyMessageHandler>(testMessage);

        // Assert
        handler.Received(1).CanHandle(Arg.Any<Message>());
        handler.Received(1).Handle(Arg.Any<Message>());
    }

    [Fact]
    public void DispatchDoesNotCallHandleWhenItCantHandleMessage()
    {
        // Arrange
        var handler = Substitute.For<DummyMessageHandler>();
        handler.CanHandle(Arg.Any<Message>()).Returns(false);

        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance);
        var testMessage = new Message();

        // Act
        dispatcher.AddHandler<DummyMessageHandler>(() => handler);
        dispatcher.DispatchMessage<DummyMessageHandler>(testMessage);

        // Assert
        handler.Received(1).CanHandle(Arg.Is<Message>(m => m == testMessage));
        handler.DidNotReceiveWithAnyArgs().Handle(Arg.Any<Message>());
    }

    [Fact]
    public void DispatchCatchesExceptionOccuringInHandle()
    {
        // Arrange
        var handler = Substitute.For<DummyMessageHandler>();
        handler.CanHandle(Arg.Any<Message>()).Returns(true);
        handler.When(h => h.Handle(Arg.Any<Message>())).Do(_ => throw new Exception());

        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance);
        var testMessage = new Message();

        // Act
        dispatcher.AddHandler<DummyMessageHandler>(() => handler);
        dispatcher.DispatchMessage<DummyMessageHandler>(testMessage);

        // Assert
        handler.Received(1).CanHandle(Arg.Any<Message>());
        handler.Received(1).Handle(Arg.Any<Message>());
    }

    [Fact]
    public void DispatchCallsAction()
    {
        // Arrange
        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance);
        var testMessage = new Message();

        // Act
        Message? actionMessage = null;
        dispatcher.DispatchMessage(msg => actionMessage = msg, testMessage);

        // Assert
        Assert.NotNull(actionMessage);
        Assert.Equal(testMessage, actionMessage);
    }

    [Fact]
    public void DispatchCatchesExceptionOccuringInAction()
    {
        // Arrange
        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance);
        var testMessage = new Message();

        // Act
        Message? actionMessage = null;
        dispatcher.DispatchMessage(
            msg =>
            {
                actionMessage = msg;
                throw new Exception();
            },
            testMessage);

        // Assert
        Assert.NotNull(actionMessage);
        Assert.Equal(testMessage, actionMessage);
    }

    public abstract class DummyMessageHandler : IMessageHandler
    {
        public virtual bool CanHandle(Message message) => throw new NotImplementedException();
        public virtual void Handle(Message message) => throw new NotImplementedException();
    }
}