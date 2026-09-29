// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using System.Threading.Channels;
using NATS.Client.Core;
using NSubstitute;
using SAF.Messaging.Contracts;
using TestUtilities.WireFormat;
using Xunit;
using INatsSubscriptionManager = SAF.Messaging.Nats.INatsSubscriptionManager;

/// <summary>
/// Records the NATS wire format as an old SAF node sees it. Verified to be identical in 9.0.1,
/// 10.0.3 and 11.0.0-alpha.9: the body is the payload string, there is no envelope and no header.
/// A failure here means an old node would read something different than it does today.
/// </summary>
public class NatsWireFormatGoldenTests
{
    public static TheoryData<string> ReferenceIds
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var id in WireFormatReferenceMessages.Ids) data.Add(id);
            return data;
        }
    }

    [Theory]
    [InlineData(WireFormatReferenceMessages.TopicOnly, "saf.wire.topic-only")]
    [InlineData(WireFormatReferenceMessages.TextPayload, "saf.wire.text")]
    [InlineData(WireFormatReferenceMessages.CustomProperties, "saf.wire.props")]
    [InlineData(WireFormatReferenceMessages.EmptyPayload, "saf.wire.empty")]
    [InlineData(WireFormatReferenceMessages.UnicodeAndEscapes, "saf.wire.unicode")]
    [InlineData(WireFormatReferenceMessages.NullPropertyValue, "saf.wire.null-prop")]
    public void Publish_MapsTopicToSubject(string id, string expectedSubject)
    {
        var (subject, _) = Publish(WireFormatReferenceMessages.Create(id));

        Assert.Equal(expectedSubject, subject);
    }

    /// <summary>
    /// The body is the raw payload string - no envelope, no encoding, not even for an empty or null payload.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReferenceIds))]
    public void Publish_SendsPayloadAsBody(string id)
    {
        var message = WireFormatReferenceMessages.Create(id);

        var (_, body) = Publish(message);

        Assert.Equal(message.Payload, body);
    }

    /// <summary>
    /// Documents today's data loss: custom properties never reach the wire, in any SAF version since 9.x.
    /// </summary>
    [Fact]
    public void Publish_DropsCustomProperties()
    {
        var message = WireFormatReferenceMessages.Create(WireFormatReferenceMessages.CustomProperties);

        var (_, body) = Publish(message);

        Assert.Equal("""{"value":42}""", body);
        Assert.DoesNotContain("replyTo", body);
    }

    [Theory]
    [InlineData("saf.wire.text", "saf/wire/text")]
    [InlineData("saf.wire.topic-only", "saf/wire/topic-only")]
    public async Task Subscribe_MapsSubjectToTopicAndBodyToPayload(string subject, string expectedTopic)
    {
        var received = await ReceiveAsync(subject, "body");

        Assert.Equal(expectedTopic, received.Topic);
        Assert.Equal("body", received.Payload);
        Assert.Null(received.CustomProperties);
    }

    private static (string? Subject, string? Body) Publish(Message message)
    {
        var dispatcher = Substitute.For<IServiceMessageDispatcher>();
        var client = Substitute.For<INatsClient>();
        var subscriptionManager = Substitute.For<INatsSubscriptionManager>();

        CreateMessaging(client, subscriptionManager, dispatcher).Publish(message);

        var call = client.ReceivedCalls().FirstOrDefault(c => c.GetMethodInfo().Name == "PublishAsync");
        Assert.NotNull(call);

        var arguments = call!.GetArguments();
        return ((string?)arguments[0], (string?)arguments[1]);
    }

    private static async Task<Message> ReceiveAsync(string subject, string body)
    {
        var dispatcher = Substitute.For<IServiceMessageDispatcher>();
        var client = Substitute.For<INatsClient>();
        var subscriptionManager = Substitute.For<INatsSubscriptionManager>();

        var channel = Channel.CreateUnbounded<NatsMsg<string>>();
        var subscription = Substitute.For<INatsSub<string>>();
        subscription.Msgs.Returns(channel.Reader);
        client.Connection
            .SubscribeCoreAsync<string>(subject: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(subscription);

        var received = new TaskCompletionSource<Message>();
        dispatcher.When(d => d.DispatchMessage(Arg.Any<Action<Message>>(), Arg.Any<Message>()))
            .Do(ci => received.TrySetResult(ci.ArgAt<Message>(1)));

        CreateMessaging(client, subscriptionManager, dispatcher).Subscribe(">", _ => { });

        await channel.Writer.WriteAsync(
            new NatsMsg<string>(subject, null, body.Length, null, body, null),
            TestContext.Current.CancellationToken);

        return await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static Messaging CreateMessaging(INatsClient client, INatsSubscriptionManager subscriptionManager,
        IServiceMessageDispatcher dispatcher)
        => new(null, client, subscriptionManager, new NatsInputRouteTranslator(), new NatsOutputRouteTranslator(), dispatcher);
}
