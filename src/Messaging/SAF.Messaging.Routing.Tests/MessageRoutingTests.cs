// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Threading;
using NSubstitute;
using SAF.Common;
using Xunit;

namespace SAF.Messaging.Routing.Tests
{
    public class MessageRoutingTest
    {
        [Fact]
        public void RunMessaging()
        {
            var messageRouting = Substitute.For<IMessageRouting>();
            Messaging messaging = new(null, new IMessageRouting[] { messageRouting });

            var messageHandler = Substitute.For<IMessageHandler>();
            messageHandler.CanHandle(Arg.Any<Message>()).Returns(true);

            var id = (Guid)messaging.Subscribe<IMessageHandler>();
            messageRouting.Received().Subscribe<IMessageHandler>(Arg.Any<string>());
            messageRouting.ClearReceivedCalls();

            Action<Message> handler = m =>
            {
                var req = m.Payload;
                Thread.Sleep(4);
            };
            var id2 = (Guid)messaging.Subscribe(handler);
            messageRouting.Received().Subscribe(Arg.Any<string>(), handler);
            messageRouting.ClearReceivedCalls();

            Message msg = new();
            msg.Topic = "Top";
            msg.Payload = "Payxx";
            messaging.Publish(msg);
            messageRouting.Received().Publish(Arg.Is<Message>(msg));
            messageRouting.ClearReceivedCalls();

            messaging.Unsubscribe(id);
        }

        [Theory]
        [InlineData("test/this/topic")]
        [InlineData("test/any/*/topic")]
        [InlineData("test/this/subtopic/*")]
        public void NullSubscriptionPatternAlwaysCallsSubscribeOk(string pattern)
        {
            void TestHandler(Message msg) { }
            var subId = new object();

            var messaging = Substitute.For<IMessagingInfrastructure>();
            messaging.Subscribe(Arg.Is(pattern), Arg.Is((Action<Message>)TestHandler)).Returns(subId);

            var routing = new MessageRouting(messaging);

            var sub = routing.Subscribe(pattern, TestHandler);
            messaging.Received().Subscribe(Arg.Is(pattern), Arg.Is((Action<Message>)TestHandler));

            sub.Dispose();
            messaging.Received().Unsubscribe(subId);
        }

        [Theory]
        [InlineData("test/this/topic")]
        [InlineData("test/any/*/topic")]
        [InlineData("test/this/subtopic/*")]
        public void EmptySubscriptionPatternAlwaysCallsSubscribeOk(string pattern)
        {
            void TestHandler(Message msg) { }
            var subId = new object();

            var messaging = Substitute.For<IMessagingInfrastructure>();
            messaging.Subscribe(Arg.Is(pattern), Arg.Is((Action<Message>)TestHandler)).Returns(subId);

            var routing = new MessageRouting(messaging)
            {
                SubscriptionPatterns = Array.Empty<string>()
            };
            var sub = routing.Subscribe(pattern, TestHandler);
            messaging.Received().Subscribe(Arg.Is(pattern), Arg.Is((Action<Message>)TestHandler));

            sub.Dispose();
            messaging.Received().Unsubscribe(subId);
        }

        [Theory]
        [InlineData("test/this/topic", "test/this/topic")]
        [InlineData("test/any/*/topic", "test/any/of/this/topic")]
        [InlineData("test/this/subtopic/*", "test/this/subtopic/of/this/topic")]
        [InlineData("test/this/subtopic/*", "test/this/subtopic/of/any/*/topic")]
        [InlineData("test/this/subtopic/*", "test/this/subtopic/with/any/*")]
        [InlineData("*/this/topic", "test/this/topic")]
        [InlineData("test/this/*/*/that", "test/this/*/*/that")]
        public void MatchingSubscriptionPatternCallsSubscribeOk(string routingPattern, string pattern)
        {
            void TestHandler(Message msg) { }
            var subId = new object();

            var messaging = Substitute.For<IMessagingInfrastructure>();
            messaging.Subscribe(Arg.Is(pattern), Arg.Is((Action<Message>)TestHandler)).Returns(subId);

            var routing = new MessageRouting(messaging)
            {
                SubscriptionPatterns = new []{ routingPattern }
            };
            var sub = routing.Subscribe(pattern, TestHandler);
            messaging.Received().Subscribe(Arg.Is(pattern), Arg.Is((Action<Message>)TestHandler));

            sub.Dispose();
            messaging.Received().Unsubscribe(subId);
        }

        [Theory]
        [InlineData("test/any/*/topic", "test/any/*")]
        [InlineData("test/this/subtopic/*", "test/this/*")]
        [InlineData("*/this/topic", "*/this/topic")]
        [InlineData("test/this/*", "*/subtopic")]
        [InlineData("test/this/topic/*", "test/this/*/*")]
        [InlineData("test/this/*/*/that", "test/this/*/*/that")]
        public void MatchingRoutingPatternCallsSubscribeOk(string routingPattern, string pattern)
        {
            void TestHandler(Message msg) { }
            var subId = new object();

            var messaging = Substitute.For<IMessagingInfrastructure>();
            messaging.Subscribe(Arg.Is(routingPattern), Arg.Is((Action<Message>)TestHandler)).Returns(subId);

            var routing = new MessageRouting(messaging)
            {
                SubscriptionPatterns = new[] { routingPattern }
            };
            var sub = routing.Subscribe(pattern, TestHandler);
            messaging.Received().Subscribe(Arg.Is(routingPattern), Arg.Is((Action<Message>)TestHandler));

            sub.Dispose();
            messaging.Received().Unsubscribe(subId);
        }

        [Theory]
        [InlineData("test/this/topic")]
        [InlineData("test/any/topic")]
        [InlineData("test/this/subtopic")]
        public void NullPublishPatternAlwaysCallsPublishOk(string topic)
        {
            var message = CreateTestMessage(topic);
            var messaging = Substitute.For<IMessagingInfrastructure>();

            var routing = new MessageRouting(messaging);
            routing.Publish(message);

            messaging.Received().Publish(Arg.Is(message));
        }

        [Theory]
        [InlineData("test/this/topic")]
        [InlineData("test/any/topic")]
        [InlineData("test/this/subtopic")]
        public void EmptyPublishPatternAlwaysCallsPublishOk(string topic)
        {
            var message = CreateTestMessage(topic);
            var messaging = Substitute.For<IMessagingInfrastructure>();

            var routing = new MessageRouting(messaging)
            {
                PublishPatterns = Array.Empty<string>()
            };
            routing.Publish(message);

            messaging.Received().Publish(Arg.Is(message));
        }

        [Theory]
        [InlineData("test/this/topic", "test/this/topic")]
        [InlineData("test/any/*/topic", "test/any/of/this/topic")]
        [InlineData("test/this/subtopic/*", "test/this/subtopic/of/this/topic")]
        [InlineData("test/this/subtopic/*", "test/this/subtopic/of/any/topic")]
        [InlineData("test/this/subtopic/*", "test/this/subtopic/with/any/data")]
        [InlineData("*/this/topic", "test/this/topic")]
        [InlineData("test/this/*/*/data", "test/this/subtopic/with/data")]
        public void MatchingPublishPatternCallsPublishOk(string routingPattern, string topic)
        {
            var message = CreateTestMessage(topic);
            var messaging = Substitute.For<IMessagingInfrastructure>();

            var routing = new MessageRouting(messaging)
            {
                PublishPatterns = new []{ routingPattern }
            };
            routing.Publish(message);

            messaging.Received().Publish(Arg.Is(message));
        }

        private static Message CreateTestMessage(string topic)
            => new()
            {
                Topic = topic,
                Payload = "test"
            };
    }
}