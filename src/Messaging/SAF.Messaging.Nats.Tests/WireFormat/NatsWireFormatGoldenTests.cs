// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using System.Threading.Channels;
using NATS.Client.Core;
using NSubstitute;
using SAF.Messaging.Contracts;
using TestUtilities.WireFormat;
using SAF.Messaging.Nats.WireFormat;
using Xunit;
using INatsSubscriptionManager = SAF.Messaging.Nats.INatsSubscriptionManager;

/// <summary>
/// Records the NATS wire format. 9.0.1, 10.0.3 and 11.0.0-alpha.9 send the payload string as body,
/// without envelope and without headers, and ignore headers on receipt. Since then custom properties
/// travel in SAF headers, while the body stays the payload. A failure here means an old node would
/// read something different than it does today.
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
        var (subject, _, _) = Publish(WireFormatReferenceMessages.Create(id));

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

        var (_, body, _) = Publish(message);

        Assert.Equal(message.Payload, body);
    }

    /// <summary>
    /// Without custom properties nothing changes on the wire, which also keeps NATS servers before 2.2 working.
    /// </summary>
    [Theory]
    [InlineData(WireFormatReferenceMessages.TopicOnly)]
    [InlineData(WireFormatReferenceMessages.TextPayload)]
    [InlineData(WireFormatReferenceMessages.EmptyPayload)]
    [InlineData(WireFormatReferenceMessages.UnicodeAndEscapes)]
    public void Publish_SendsNoHeaders_WithoutCustomProperties(string id)
    {
        var (_, _, headers) = Publish(WireFormatReferenceMessages.Create(id));

        Assert.Null(headers);
    }

    /// <summary>
    /// Up to 11.0.0-alpha.9 custom properties never reached the wire. Now they travel in headers, which
    /// older nodes ignore, so the body they read stays the same.
    /// </summary>
    [Theory]
    [InlineData(WireFormatReferenceMessages.CustomProperties,
        """{"customProperties":[{"name":"replyTo","value":"saf/wire/reply"},{"name":"correlationId","value":"c3f1a0"}]}""")]
    [InlineData(WireFormatReferenceMessages.NullPropertyValue,
        """{"customProperties":[{"name":"flag"}]}""")]
    public void Publish_SendsCustomPropertiesAsHeaders(string id, string expectedMetadata)
    {
        var message = WireFormatReferenceMessages.Create(id);

        var (_, body, headers) = Publish(message);

        Assert.Equal(message.Payload, body);
        Assert.NotNull(headers);
        Assert.Equal(2, headers!.Count);
        Assert.Equal("2.0.0", headers[NatsHeaderNames.Version].ToString());
        Assert.Equal(expectedMetadata, headers[NatsHeaderNames.Metadata].ToString());
    }

    [Theory]
    [InlineData("saf.wire.text", "saf/wire/text")]
    [InlineData("saf.wire.topic-only", "saf/wire/topic-only")]
    public async Task Subscribe_MapsSubjectToTopicAndBodyToPayload(string subject, string expectedTopic)
    {
        var received = await ReceiveAsync(Msg(subject, "body"));

        Assert.Equal(expectedTopic, received.Topic);
        Assert.Equal("body", received.Payload);
        Assert.Null(received.CustomProperties);
    }

    [Fact]
    public async Task Subscribe_ReadsCustomPropertiesFromHeaders()
    {
        var expected = WireFormatReferenceMessages.Create(WireFormatReferenceMessages.CustomProperties);
        var (_, body, headers) = Publish(expected);

        var received = await ReceiveAsync(Msg("saf.wire.props", body, headers));

        Assert.Equal(expected.Topic, received.Topic);
        Assert.Equal(expected.Payload, received.Payload);
        Assert.Equal(["replyTo", "correlationId"], received.CustomProperties!.Select(p => p.Name));
        Assert.Equal(["saf/wire/reply", "c3f1a0"], received.CustomProperties!.Select(p => p.Value));
    }

    [Fact]
    public async Task Subscribe_IgnoresForeignHeadersWithoutVersion()
    {
        var received = await ReceiveAsync(Msg("saf.wire.text", "body", new NatsHeaders { { "trace-id", "1" } }));

        Assert.Equal("body", received.Payload);
        Assert.Null(received.CustomProperties);
    }

    [Theory]
    [InlineData("3.0.0", """{"customProperties":[]}""")]
    [InlineData("2.0.0", "not json")]
    public async Task Subscribe_DropsMessagesItCannotRead(string version, string metadata)
    {
        var unreadable = Msg("saf.wire.text", "dropped",
            new NatsHeaders { { NatsHeaderNames.Version, version }, { NatsHeaderNames.Metadata, metadata } });

        var received = await ReceiveAsync(unreadable, Msg("saf.wire.text", "next"));

        Assert.Equal("next", received.Payload);
    }

    private static NatsMsg<string> Msg(string subject, string? body, NatsHeaders? headers = null)
        => new(subject, null, body?.Length ?? 0, headers, body, null);

    private static (string? Subject, string? Body, NatsHeaders? Headers) Publish(Message message)
    {
        var dispatcher = Substitute.For<IServiceMessageDispatcher>();
        var client = Substitute.For<INatsClient>();
        var subscriptionManager = Substitute.For<INatsSubscriptionManager>();

        CreateMessaging(client, subscriptionManager, dispatcher).Publish(message);

        var call = client.ReceivedCalls().FirstOrDefault(c => c.GetMethodInfo().Name == "PublishAsync");
        Assert.NotNull(call);

        var arguments = call!.GetArguments();
        return ((string?)arguments[0], (string?)arguments[1], (NatsHeaders?)arguments[2]);
    }

    /// <summary>
    /// Feeds the messages into the subscription and returns the first one that is dispatched.
    /// </summary>
    private static async Task<Message> ReceiveAsync(params NatsMsg<string>[] messages)
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

        foreach (var msg in messages)
        {
            await channel.Writer.WriteAsync(msg, TestContext.Current.CancellationToken);
        }

        return await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static Messaging CreateMessaging(INatsClient client, INatsSubscriptionManager subscriptionManager,
        IServiceMessageDispatcher dispatcher)
        => new(null, client, subscriptionManager, new NatsInputRouteTranslator(), new NatsOutputRouteTranslator(), dispatcher,
            TestWireFormat.Writer(), TestWireFormat.Reader());
}
