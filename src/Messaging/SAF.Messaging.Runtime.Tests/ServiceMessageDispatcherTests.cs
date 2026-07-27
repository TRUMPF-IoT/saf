// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Runtime.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SAF.Messaging.Contracts;
using Xunit;

public class ServiceMessageDispatcherTests
{
    [Fact]
    public void DispatchMessage_WhenHandlerCanHandleMessage_CallsHandle()
    {
        var handler = Substitute.For<IMessageHandler>();
        handler.CanHandle(Arg.Any<Message>()).Returns(true);

        var resolver = Substitute.For<IMessageHandlerResolver>();
        resolver.Resolve(typeof(TestHandler)).Returns(handler);

        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance, [resolver]);
        var message = new Message { Topic = "topic/1", Payload = "payload" };

        dispatcher.DispatchMessage(typeof(TestHandler), message);

        handler.Received(1).Handle(message);
    }

    [Fact]
    public void DispatchMessage_WhenHandlerCannotHandleMessage_DoesNotCallHandle()
    {
        var handler = Substitute.For<IMessageHandler>();
        handler.CanHandle(Arg.Any<Message>()).Returns(false);

        var resolver = Substitute.For<IMessageHandlerResolver>();
        resolver.Resolve(typeof(TestHandler)).Returns(handler);

        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance, [resolver]);

        dispatcher.DispatchMessage(typeof(TestHandler), new Message { Topic = "topic/2" });

        handler.DidNotReceive().Handle(Arg.Any<Message>());
    }

    [Fact]
    public void DispatchMessage_WhenHandlerTypeIsUnknown_CachesNegativeResolverResult()
    {
        var resolver = Substitute.For<IMessageHandlerResolver>();
        resolver.Resolve(typeof(TestHandler)).Returns((IMessageHandler?)null);

        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance, [resolver]);
        var message = new Message { Topic = "topic/3" };

        dispatcher.DispatchMessage(typeof(TestHandler), message);
        dispatcher.DispatchMessage(typeof(TestHandler), message);

        resolver.Received(1).Resolve(typeof(TestHandler));
    }

    [Fact]
    public void DispatchMessage_WhenCalledWithGenericHandler_DispatchesUsingResolvedHandler()
    {
        var handler = Substitute.For<IMessageHandler>();
        handler.CanHandle(Arg.Any<Message>()).Returns(true);

        var resolver = Substitute.For<IMessageHandlerResolver>();
        resolver.Resolve(typeof(TestHandler)).Returns(handler);

        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance, [resolver]);
        var message = new Message { Topic = "topic/4", Payload = "payload" };

        dispatcher.DispatchMessage<TestHandler>(message);

        handler.Received(1).Handle(message);
    }

    [Fact]
    public void DispatchMessage_WhenResolverFoundOnce_UsesResolverCacheForSubsequentDispatches()
    {
        var firstResolver = Substitute.For<IMessageHandlerResolver>();
        var secondResolver = Substitute.For<IMessageHandlerResolver>();
        var handler = Substitute.For<IMessageHandler>();
        handler.CanHandle(Arg.Any<Message>()).Returns(true);

        firstResolver.Resolve(typeof(TestHandler)).Returns((IMessageHandler?)null);
        secondResolver.Resolve(typeof(TestHandler)).Returns(handler);

        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance, [firstResolver, secondResolver]);
        var message = new Message { Topic = "topic/5" };

        dispatcher.DispatchMessage(typeof(TestHandler), message);
        dispatcher.DispatchMessage(typeof(TestHandler), message);

        firstResolver.Received(1).Resolve(typeof(TestHandler));
        secondResolver.Received(2).Resolve(typeof(TestHandler));
    }

    [Fact]
    public void DispatchMessage_WhenLambdaHandlerThrows_DoesNotThrow()
    {
        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance, []);
        var message = new Message { Topic = "topic/6" };

        var exception = Record.Exception(() => dispatcher.DispatchMessage(_ => throw new InvalidOperationException("boom"), message));

        Assert.Null(exception);
    }

    [Fact]
    public void DispatchMessage_WhenResolvedHandlerThrows_DoesNotThrow()
    {
        var handler = Substitute.For<IMessageHandler>();
        handler.CanHandle(Arg.Any<Message>()).Returns(true);
        handler
            .When(h => h.Handle(Arg.Any<Message>()))
            .Do(_ => throw new InvalidOperationException("boom"));

        var resolver = Substitute.For<IMessageHandlerResolver>();
        resolver.Resolve(typeof(TestHandler)).Returns(handler);

        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance, [resolver]);
        var message = new Message { Topic = "topic/7" };

        var exception = Record.Exception(() => dispatcher.DispatchMessage(typeof(TestHandler), message));

        Assert.Null(exception);
    }

    private sealed class TestHandler : IMessageHandler
    {
        public bool CanHandle(Message message) => true;

        public void Handle(Message message)
        {
        }
    }
}
