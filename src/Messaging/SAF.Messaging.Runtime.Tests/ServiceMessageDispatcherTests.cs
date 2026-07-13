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
        resolver.Resolve(typeof(TestHandler)).Returns(_ => throw new InvalidOperationException("unsupported"));

        var dispatcher = new ServiceMessageDispatcher(NullLogger<ServiceMessageDispatcher>.Instance, [resolver]);
        var message = new Message { Topic = "topic/3" };

        dispatcher.DispatchMessage(typeof(TestHandler), message);
        dispatcher.DispatchMessage(typeof(TestHandler), message);

        resolver.Received(1).Resolve(typeof(TestHandler));
    }

    private sealed class TestHandler : IMessageHandler
    {
        public bool CanHandle(Message message) => true;

        public void Handle(Message message)
        {
        }
    }
}
