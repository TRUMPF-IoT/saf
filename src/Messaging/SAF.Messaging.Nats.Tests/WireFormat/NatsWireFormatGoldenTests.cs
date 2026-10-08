// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using SAF.Messaging.Contracts;
using TestUtilities;
using TestUtilities.WireFormat;
using SAF.Messaging.Nats.WireFormat;
using Xunit;
using INatsSubscriptionManager = SAF.Messaging.Nats.INatsSubscriptionManager;
using static TestWireFormat;

/// <summary>
/// Records the NATS wire format. 9.0.1, 10.0.3 and 11.0.0-alpha.9 send the payload string as body,
/// without envelope and without headers, and ignore headers on receipt. Since then custom properties
/// and accepted reply formats travel in SAF headers, while the body stays the payload. A binary payload is the body
/// of a version 3 message. A failure here means an old node would read something different than it does today.
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

    public static TheoryData<string> BinaryIds
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var id in WireFormatReferenceMessages.BinaryIds) data.Add(id);
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
    [InlineData(WireFormatReferenceMessages.ReplyFormats, "saf.wire.reply-formats")]
    [InlineData(WireFormatReferenceMessages.Binary, "saf.wire.binary")]
    public void Publish_MapsTopicToSubject(string id, string expectedSubject)
    {
        var (subject, _, _) = Publish(WireFormatReferenceMessages.Create(id));

        Assert.Equal(expectedSubject, subject);
    }

    /// <summary>
    /// The body is the raw payload string - no envelope, no encoding, not even for an empty or null payload.
    /// It goes to NATS.Net as a string, which encodes it as UTF-8 like in every earlier SAF version.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReferenceIds))]
    public void Publish_SendsPayloadAsBody(string id)
    {
        var message = WireFormatReferenceMessages.Create(id);

        var (_, body, _) = Publish(message);

        Assert.Equal(message.Payload, (string?)body);
    }

    /// <summary>
    /// Switching binary payloads off leaves text messages unchanged.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReferenceIds))]
    public void Publish_SendsTheSameTextMessages_WhenBinaryPayloadsAreDisabled(string id)
    {
        var message = WireFormatReferenceMessages.Create(id);
        var enabled = Publish(message);

        var disabled = Publish(message, enableBinaryPayloads: false);

        Assert.Equal(enabled.Body, disabled.Body);
        Assert.Equal(enabled.Headers?.Select(h => (h.Key, h.Value.ToString())), disabled.Headers?.Select(h => (h.Key, h.Value.ToString())));
    }

    /// <summary>
    /// Without metadata nothing changes on the wire, which also keeps NATS servers before 2.2 working.
    /// </summary>
    [Theory]
    [InlineData(WireFormatReferenceMessages.TopicOnly)]
    [InlineData(WireFormatReferenceMessages.TextPayload)]
    [InlineData(WireFormatReferenceMessages.EmptyPayload)]
    [InlineData(WireFormatReferenceMessages.UnicodeAndEscapes)]
    public void Publish_SendsNoHeaders_WithoutMetadata(string id)
    {
        var (_, _, headers) = Publish(WireFormatReferenceMessages.Create(id));

        Assert.Null(headers);
    }

    /// <summary>
    /// Up to 11.0.0-alpha.9 custom properties never reached the wire. Now they travel in headers together
    /// with the accepted reply formats. Older nodes ignore the headers, so the body they read stays the same.
    /// </summary>
    [Theory]
    [InlineData(WireFormatReferenceMessages.CustomProperties,
        """{"customProperties":[{"name":"replyTo","value":"saf/wire/reply"},{"name":"correlationId","value":"c3f1a0"}]}""")]
    [InlineData(WireFormatReferenceMessages.NullPropertyValue,
        """{"customProperties":[{"name":"flag"}]}""")]
    [InlineData(WireFormatReferenceMessages.ReplyFormats,
        """{"acceptedReplyFormats":3}""")]
    public void Publish_SendsMetadataAsHeaders(string id, string expectedMetadata)
    {
        var message = WireFormatReferenceMessages.Create(id);

        var (_, body, headers) = Publish(message);

        Assert.Equal(message.Payload, body);
        Assert.NotNull(headers);
        Assert.Equal(2, headers!.Count);
        Assert.Equal("2.0.0", headers[NatsHeaderNames.Version].ToString());
        Assert.Equal(expectedMetadata, headers[NatsHeaderNames.Metadata].ToString());
    }

    /// <summary>
    /// The binary payload itself is the body, not a copy. The text payload and all other fields travel in
    /// <c>saf-meta</c>, which is left out if none is set.
    /// </summary>
    [Theory]
    [MemberData(nameof(BinaryIds))]
    public void Publish_SendsBinaryPayloadAsBody(string id)
    {
        var message = WireFormatReferenceMessages.Create(id);

        var (_, body, headers) = Publish(message);

        Assert.Same(message.BinaryPayload, body);
        Assert.Equal("3.0.0", headers![NatsHeaderNames.Version].ToString());
        var expectedMetadata = GoldenBinaryMetadata(id);
        Assert.Equal(expectedMetadata == null ? 1 : 2, headers.Count);
        if (expectedMetadata != null) Assert.Equal(expectedMetadata, headers[NatsHeaderNames.Metadata].ToString());
    }

    /// <summary>
    /// Nodes before SAF 11 read a binary body as corrupt text, so the operator can switch binary payloads off.
    /// </summary>
    [Theory]
    [MemberData(nameof(BinaryIds))]
    public void Publish_DropsBinaryPayloadsAndLogsAnError_WhenDisabled(string id)
    {
        var client = Substitute.For<INatsClient>();
        var logger = Substitute.For<MockLogger<Messaging>>();

        CreateMessaging(client, Substitute.For<INatsSubscriptionManager>(), Substitute.For<IServiceMessageDispatcher>(), enableBinaryPayloads: false, logger)
            .Publish(WireFormatReferenceMessages.Create(id));

        Assert.DoesNotContain(client.ReceivedCalls(), c => c.GetMethodInfo().Name == "PublishAsync");
        logger.AssertLogged(LogLevel.Error, m => m.Contains("saf/wire/") && m.Contains("EnableBinaryPayloads"));
    }

    /// <summary>
    /// A node before SAF 11 ignores the headers and reads the body as a UTF-8 string, with every byte that is no
    /// valid UTF-8 replaced.
    /// </summary>
    [Fact]
    public void OldNodes_ReadABinaryBodyAsCorruptText()
    {
        var (_, body, _) = Publish(WireFormatReferenceMessages.Create(WireFormatReferenceMessages.Binary));

        var text = Encoding.UTF8.GetString((byte[])body!);

        Assert.Contains((char)0xFFFD, text);
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

    [Theory]
    [MemberData(nameof(ReferenceIds))]
    [MemberData(nameof(BinaryIds))]
    public async Task Subscribe_ReadsWhatPublishSends(string id)
    {
        var expected = WireFormatReferenceMessages.Create(id);
        var (subject, body, headers) = Publish(expected);

        var received = await ReceiveAsync(Msg(subject!, body as byte[] ?? Utf8Bytes((string?)body), headers));

        Assert.Equal(expected.Topic, received.Topic);
        // NATS delivers an empty body as no body: an empty text payload arrives as null, in every SAF version.
        Assert.Equal(expected.Payload == "" ? null : expected.Payload, received.Payload);
        Assert.Equal(expected.BinaryPayload, received.BinaryPayload);
        Assert.Equal(expected.AcceptedReplyFormats, received.AcceptedReplyFormats);
        Assert.Equal(expected.CustomProperties?.Select(p => (p.Name, p.Value)), received.CustomProperties?.Select(p => (p.Name, p.Value)));
    }

    /// <summary>
    /// Text is decoded exactly like NATS.Net decodes a string: invalid UTF-8 is replaced, a byte order mark is kept.
    /// </summary>
    [Fact]
    public async Task Subscribe_DecodesTheBodyLikeAStringSubscription()
    {
        byte[] body = [0xEF, 0xBB, 0xBF, 0x53, 0x00, 0xFF, 0xFE, 0x80];

        var received = await ReceiveAsync(Msg("saf.wire.text", body));

        Assert.Equal(Encoding.UTF8.GetString(body), received.Payload);
    }

    [Fact]
    public async Task Subscribe_IgnoresForeignHeadersWithoutVersion()
    {
        var received = await ReceiveAsync(Msg("saf.wire.text", "body", new NatsHeaders { { "trace-id", "1" } }));

        Assert.Equal("body", received.Payload);
        Assert.Null(received.CustomProperties);
    }

    [Theory]
    [InlineData("4.0.0", """{"customProperties":[]}""")]
    [InlineData("2.0.0", "not json")]
    [InlineData("3.0.0", "not json")]
    public async Task Subscribe_DropsMessagesItCannotRead(string version, string metadata)
    {
        var unreadable = Msg("saf.wire.text", "dropped",
            new NatsHeaders { { NatsHeaderNames.Version, version }, { NatsHeaderNames.Metadata, metadata } });

        var received = await ReceiveAsync(unreadable, Msg("saf.wire.text", "next"));

        Assert.Equal("next", received.Payload);
    }

    private static string? GoldenBinaryMetadata(string id) => id switch
    {
        WireFormatReferenceMessages.Binary => null,
        // JavaScriptEncoder.Default escapes the quotes inside the payload.
        WireFormatReferenceMessages.TextAndBinary =>
            """{"payload":"{\u0022name\u0022:\u0022chunk\u0022}","customProperties":[{"name":"index","value":"3"}]}""",
        WireFormatReferenceMessages.EmptyBinary => null,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "No recorded sample for this reference message.")
    };

    private static (string? Subject, object? Body, NatsHeaders? Headers) Publish(Message message, bool enableBinaryPayloads = true)
    {
        var dispatcher = Substitute.For<IServiceMessageDispatcher>();
        var client = Substitute.For<INatsClient>();
        var subscriptionManager = Substitute.For<INatsSubscriptionManager>();

        CreateMessaging(client, subscriptionManager, dispatcher, enableBinaryPayloads).Publish(message);

        var call = client.ReceivedCalls().FirstOrDefault(c => c.GetMethodInfo().Name == "PublishAsync");
        Assert.NotNull(call);
        // Text goes to NATS.Net as a string, binary payloads as bytes.
        Assert.Equal(message.BinaryPayload == null ? typeof(string) : typeof(byte[]), call!.GetMethodInfo().GetGenericArguments()[0]);

        var arguments = call.GetArguments();
        return ((string?)arguments[0], arguments[1], (NatsHeaders?)arguments[2]);
    }

    /// <summary>
    /// Feeds the messages into the subscription and returns the first one that is dispatched.
    /// </summary>
    private static async Task<Message> ReceiveAsync(params NatsMsg<NatsMemoryOwner<byte>>[] messages)
    {
        var dispatcher = Substitute.For<IServiceMessageDispatcher>();
        var client = Substitute.For<INatsClient>();
        var subscriptionManager = Substitute.For<INatsSubscriptionManager>();

        var channel = Channel.CreateUnbounded<NatsMsg<NatsMemoryOwner<byte>>>();
        var subscription = Substitute.For<INatsSub<NatsMemoryOwner<byte>>>();
        subscription.Msgs.Returns(channel.Reader);
        client.Connection
            .SubscribeCoreAsync<NatsMemoryOwner<byte>>(subject: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
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
        IServiceMessageDispatcher dispatcher, bool enableBinaryPayloads = true, ILogger<Messaging>? logger = null)
        => new(logger, client, subscriptionManager, new NatsInputRouteTranslator(), new NatsOutputRouteTranslator(), dispatcher,
            Writer(enableBinaryPayloads), Reader());
}
