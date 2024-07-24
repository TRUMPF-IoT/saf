// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Tests;

using Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualBasic;
using NSubstitute;
using TestUtilities;
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
        handler.When(h => h.Handle(Arg.Any<Message>()))
            .Do(_ => throw new Exception());

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
    public void DispatchCatchesObjectDisposedExceptionOccuringInHandle()
    {
        // Arrange
        var handler = Substitute.For<DummyMessageHandler>();
        handler.CanHandle(Arg.Any<Message>()).Returns(true);
        handler.When(h => h.Handle(Arg.Any<Message>()))
            .Do(_ => throw new ObjectDisposedException("test"));

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

    [Fact]
    public void DispatchLogsErrorWhenMessageHandlerIsUnknown()
    {
        // Arrange
        var handler = Substitute.For<DummyMessageHandler>();
        handler.CanHandle(Arg.Any<Message>()).Returns(true);

        var mockLogger = Substitute.For<MockLogger<ServiceMessageDispatcher>>();

        var dispatcher = new ServiceMessageDispatcher(mockLogger);
        var testMessage = new Message();

        // Act
        dispatcher.AddHandler<DummyMessageHandler>(() => handler);
        dispatcher.DispatchMessage(typeof(DateTimeOffset), testMessage);

        // Assert
        mockLogger.Received(1).Log(LogLevel.Error, "Handler {handlerTypeFullName} unknown!", typeof(DateTimeOffset).FullName);
        handler.DidNotReceive().CanHandle(Arg.Any<Message>());
        handler.DidNotReceive().Handle(Arg.Any<Message>());
    }

    public abstract class DummyMessageHandler : IMessageHandler
    {
        public virtual bool CanHandle(Message message) => throw new NotImplementedException();
        public virtual void Handle(Message message) => throw new NotImplementedException();
    }
}