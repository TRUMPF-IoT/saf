// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Routing.Tests;

using NSubstitute;
using SAF.Messaging.Contracts;
using Xunit;

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

    [Fact]
    public void DisposingMessagingDisposesContainedRoutings()
    {
        var messageRouting = Substitute.For<IMessageRouting, IDisposable>();
        Messaging messaging = new(null, new IMessageRouting[] { messageRouting });

        messaging.Dispose();

        ((IDisposable)messageRouting).Received(1).Dispose();
    }

    [Fact]
    public void DisposingRoutingDisposesBackendMessagingInfrastructure()
    {
        var messaging = Substitute.For<IMessagingInfrastructure, IDisposable>();
        var routing = new MessageRouting(messaging);

        routing.Dispose();

        ((IDisposable)messaging).Received(1).Dispose();
    }

    [Theory]
    [InlineData("test/this/topic")]
    [InlineData("test/any/*/topic")]
    [InlineData("test/this/subtopic/*")]
    public void NullSubscriptionPatternAlwaysCallsSubscribeOk(string pattern)
    {
        var testHandler = (Message msg) => { };
        var subId = new object();

        var messaging = Substitute.For<IMessagingInfrastructure>();
        messaging.Subscribe(Arg.Is(pattern), Arg.Is(testHandler)).Returns(subId);

        var routing = new MessageRouting(messaging);

        var sub = routing.Subscribe(pattern, testHandler)!;
        messaging.Received(1).Subscribe(Arg.Is(pattern), Arg.Is(testHandler));

        sub.Dispose();
        messaging.Received().Unsubscribe(subId);
    }

    [Theory]
    [InlineData("test/this/topic")]
    [InlineData("test/any/*/topic")]
    [InlineData("test/this/subtopic/*")]
    public void EmptySubscriptionPatternAlwaysCallsSubscribeOk(string pattern)
    {
        var testHandler = (Message msg) => { };
        var subId = new object();

        var messaging = Substitute.For<IMessagingInfrastructure>();
        messaging.Subscribe(Arg.Is(pattern), Arg.Is(testHandler)).Returns(subId);

        var routing = new MessageRouting(messaging)
        {
            SubscriptionPatterns = Array.Empty<string>()
        };
        var sub = routing.Subscribe(pattern, testHandler)!;
        messaging.Received(1).Subscribe(Arg.Is(pattern), Arg.Is(testHandler));

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
        var testHandler = (Message msg) => { };
        var subId = new object();

        var messaging = Substitute.For<IMessagingInfrastructure>();
        messaging.Subscribe(Arg.Is(pattern), Arg.Is(testHandler)).Returns(subId);

        var routing = new MessageRouting(messaging)
        {
            SubscriptionPatterns = new []{ routingPattern }
        };
        var sub = routing.Subscribe(pattern, testHandler)!;
        messaging.Received(1).Subscribe(Arg.Is(pattern), Arg.Is(testHandler));

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
        var testHandler = (Message msg) => { };
        var subId = new object();

        var messaging = Substitute.For<IMessagingInfrastructure>();
        messaging.Subscribe(Arg.Is(routingPattern), Arg.Is(testHandler)).Returns(subId);

        var routing = new MessageRouting(messaging)
        {
            SubscriptionPatterns = new[] { routingPattern }
        };
        var sub = routing.Subscribe(pattern, testHandler)!;
        messaging.Received(1).Subscribe(Arg.Is(routingPattern), Arg.Is(testHandler));

        sub.Dispose();
        messaging.Received().Unsubscribe(subId);
    }

    /// <summary>
    /// The request client subscribes "{prefix}/*/requestclient/{id}" per reply prefix. A route covering the prefix
    /// subscribes exactly that pattern, which every transport can express, and none of its other patterns.
    /// </summary>
    [Theory]
    [InlineData("*")]
    [InlineData("private/*")]
    [InlineData("private/reply/*")]
    public void RequestClientReplyPatternIsSubscribedAsIs(string routingPattern)
    {
        const string pattern = "private/reply/*/requestclient/3f2a";
        var testHandler = (Message msg) => { };

        var messaging = Substitute.For<IMessagingInfrastructure>();
        var routing = new MessageRouting(messaging)
        {
            SubscriptionPatterns = [routingPattern, "sensors/*"]
        };
        routing.Subscribe(pattern, testHandler);

        messaging.Received(1).Subscribe(pattern, testHandler);
        messaging.Received(1).Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>());
    }

    [Theory]
    [InlineData("*", "*")]
    [InlineData("test/this/subtopic/*", "test/this/subtopic/*")]
    [InlineData("*/this/topic", "*/this/topic")]
    public void ExactMatchingRoutingPatternIsOnlySubscribedOnce(string pattern, string routingPattern)
    {
        var testHandler = (Message msg) => { };
        var subId = new object();

        var messaging = Substitute.For<IMessagingInfrastructure>();
        messaging.Subscribe(Arg.Is(pattern), Arg.Is(testHandler)).Returns(subId);

        var routing = new MessageRouting(messaging)
        {
            SubscriptionPatterns = [routingPattern]
        };
        var sub = routing.Subscribe(pattern, testHandler)!;
        messaging.Received(1).Subscribe(Arg.Is(pattern), Arg.Is(testHandler));

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

    [Theory]
    [InlineData(MessageFormats.Text | MessageFormats.Binary, MessageFormats.Text | MessageFormats.Binary, MessageFormats.Text | MessageFormats.Binary)]
    [InlineData(MessageFormats.Text | MessageFormats.Binary, MessageFormats.Text, MessageFormats.Text)]
    [InlineData(MessageFormats.Text, MessageFormats.Text, MessageFormats.Text)]
    public void PublishableFormatsAreThoseEveryRoutingPublishes(MessageFormats first, MessageFormats second, MessageFormats expected)
    {
        var firstRouting = Substitute.For<IMessageRouting>();
        var secondRouting = Substitute.For<IMessageRouting>();
        firstRouting.GetPublishableFormats("test/topic").Returns(first);
        secondRouting.GetPublishableFormats("test/topic").Returns(second);
        Messaging messaging = new(null, [firstRouting, secondRouting]);

        Assert.Equal(expected, messaging.GetPublishableFormats("test/topic"));
    }

    [Theory]
    [InlineData(MessageFormats.Text)]
    [InlineData(MessageFormats.Text | MessageFormats.Binary)]
    public void RoutePublishableFormats_AreThoseOfItsMessaging_ForAMatchingTopic(MessageFormats formats)
    {
        var messaging = Substitute.For<IMessagingInfrastructure>();
        messaging.GetPublishableFormats("test/topic").Returns(formats);
        var routing = new MessageRouting(messaging) { PublishPatterns = ["test/*"] };

        Assert.Equal(formats, routing.GetPublishableFormats("test/topic"));
    }

    [Fact]
    public void RoutePublishableFormats_AreAllFormats_ForATopicItDoesNotPublish()
    {
        var messaging = Substitute.For<IMessagingInfrastructure>();
        messaging.GetPublishableFormats(Arg.Any<string>()).Returns(MessageFormats.Text);
        var routing = new MessageRouting(messaging) { PublishPatterns = ["test/*"] };

        Assert.Equal(MessageFormats.Text | MessageFormats.Binary, routing.GetPublishableFormats("other/topic"));
        messaging.DidNotReceive().GetPublishableFormats(Arg.Any<string>());
    }

    private static Message CreateTestMessage(string topic)
        => new()
        {
            Topic = topic,
            Payload = "test"
        };
}


