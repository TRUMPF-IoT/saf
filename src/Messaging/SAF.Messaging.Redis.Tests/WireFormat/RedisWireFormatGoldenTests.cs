// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Messaging.Contracts;
using StackExchange.Redis;
using TestUtilities;
using TestUtilities.WireFormat;
using Xunit;

/// <summary>
/// Records the Redis wire format as an old SAF node sees it. The samples without accepted reply formats are
/// verified to be identical in 9.0.1, 10.0.3 and 11.0.0-alpha.9, so they cover every version SAF has to
/// interoperate with. Binary payloads travel in a binary frame since 11.x. A failure here means the wire format
/// changed - that is only allowed together with a deliberate bump of <see cref="RedisMessageVersion"/> and a reader
/// for the old version.
/// </summary>
public class RedisWireFormatGoldenTests
{
    // UnsafeRelaxedJsonEscaping writes these BMP characters literally to the wire.
    private const string NonAsciiLiteral = "äöüß 日本語";

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
    [MemberData(nameof(ReferenceIds))]
    public void Publish_ProducesRecordedWireFormat(string id)
    {
        var (channel, wireValue) = Publish(WireFormatReferenceMessages.Create(id));

        Assert.Equal(WireFormatReferenceMessages.Create(id).Topic, channel);
        Assert.Equal(Golden(id), wireValue.ToString());
    }

    /// <summary>
    /// Switching binary payloads off leaves text messages unchanged.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReferenceIds))]
    public void Publish_ProducesRecordedWireFormat_WhenBinaryPayloadsAreDisabled(string id)
    {
        var (_, wireValue) = Publish(WireFormatReferenceMessages.Create(id), enableBinaryPayloads: false);

        Assert.Equal(Golden(id), wireValue.ToString());
    }

    [Theory]
    [MemberData(nameof(ReferenceIds))]
    public void Subscribe_ReadsRecordedWireFormat(string id)
    {
        var expected = WireFormatReferenceMessages.Create(id);

        var received = Receive(expected.Topic, Golden(id));

        AssertMessage(expected, received);
    }

    [Theory]
    [MemberData(nameof(BinaryIds))]
    public void Publish_WritesBinaryFrame(string id)
    {
        var message = WireFormatReferenceMessages.Create(id);

        var (channel, wireValue) = Publish(message);

        Assert.Equal(message.Topic, channel);
        Assert.Equal(GoldenFrame(id), (byte[])wireValue!);
    }

    [Fact]
    public void Latest_IsTheBinaryFrameVersion()
        => Assert.Equal(RedisMessageVersion.V3, RedisMessageVersion.Latest);

    /// <summary>
    /// Magic <c>SAFB</c>, layout version 1, envelope length 57 as little-endian uint32, the envelope, the binary payload.
    /// </summary>
    [Fact]
    public void Publish_WritesBinaryFrameLayout()
    {
        var (_, wireValue) = Publish(WireFormatReferenceMessages.Create(WireFormatReferenceMessages.Binary));

        Assert.Equal(
            "534146420139000000"
            + "7B2276657273696F6E223A22332E302E30222C226D657373616765223A7B22746F706963223A227361662F776972652F62696E617279227D7D"
            + "00017F80FEFF",
            Convert.ToHexString((byte[])wireValue!));
    }

    [Theory]
    [MemberData(nameof(BinaryIds))]
    public void Subscribe_ReadsBinaryFrame(string id)
    {
        var expected = WireFormatReferenceMessages.Create(id);

        var received = Receive(expected.Topic, GoldenFrame(id));

        AssertMessage(expected, received);
    }

    /// <summary>
    /// Nodes before SAF 11 read a binary frame as corrupt text, so the operator can switch binary payloads off.
    /// </summary>
    [Theory]
    [MemberData(nameof(BinaryIds))]
    public void Publish_DropsBinaryPayloadsAndLogsAnError_WhenDisabled(string id)
    {
        var logger = Substitute.For<MockLogger<Messaging>>();
        var (messaging, subscriber, _) = CreateMessaging(enableBinaryPayloads: false, logger);

        messaging.Publish(WireFormatReferenceMessages.Create(id));

        subscriber.DidNotReceive().Publish(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>());
        logger.AssertLogged(LogLevel.Error, m => m.Contains("saf/wire/") && m.Contains("EnableBinaryPayloads"));
    }

    /// <summary>
    /// A node before SAF 11 reads the frame as text. That text is no JSON envelope, so the node hands it to its
    /// handlers as payload, with every byte that is no valid UTF-8 replaced.
    /// </summary>
    [Fact]
    public void OldNodes_ReadABinaryFrameAsCorruptText()
    {
        RedisValue frame = GoldenFrame(WireFormatReferenceMessages.Binary);

        var text = frame.ToString();

        Assert.ThrowsAny<Exception>(() => Toolbox.Serialization.JsonSerializer.Deserialize<LegacyEnvelope>(text));
        Assert.StartsWith("SAFB", text);
        Assert.Contains('\uFFFD', text);
    }

    /// <summary>
    /// V1 was declared together with V2 but never written; both versions share the same message shape.
    /// </summary>
    [Fact]
    public void Subscribe_ReadsV1EnvelopeLikeV2()
    {
        var received = Receive("saf/wire/text",
            """{"version":"1.0.0","message":{"topic":"saf/wire/text","payload":"p"}}""");

        Assert.Equal("saf/wire/text", received.Topic);
        Assert.Equal("p", received.Payload);
    }

    /// <summary>
    /// Up to 11.0.0-alpha.9 an unknown version was read like V2. Since then it is dropped, so a future format
    /// can no longer be mistaken for a corrupt old message.
    /// </summary>
    [Fact]
    public void Subscribe_DropsUnknownEnvelopeVersion()
    {
        var (messaging, subscriber, dispatcher) = CreateMessaging();
        var internalHandler = CaptureInternalHandler(messaging, subscriber);

        internalHandler(RedisChannel.Literal("saf/wire/text"),
            """{"version":"99.0.0","message":{"topic":"saf/wire/text","payload":"p"}}""");

        dispatcher.DidNotReceive().DispatchMessage(Arg.Any<Action<Message>>(), Arg.Any<Message>());
    }

    /// <summary>
    /// A node up to 11.0.0-alpha.9 reads the envelope into a type without the accepted reply formats.
    /// </summary>
    [Fact]
    public void OldNodes_IgnoreReplyFormats()
    {
        var expected = WireFormatReferenceMessages.Create(WireFormatReferenceMessages.ReplyFormats);

        var old = Toolbox.Serialization.JsonSerializer.Deserialize<LegacyEnvelope>(Golden(WireFormatReferenceMessages.ReplyFormats));

        Assert.Equal(RedisMessageVersion.V2, old!.Version);
        Assert.Equal(expected.Topic, old.Message!.Topic);
        Assert.Equal(expected.Payload, old.Message.Payload);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"foo":1}""")]
    [InlineData("")]
    public void Subscribe_FallsBackToRawValue_WhenNoEnvelopeIsRecognized(string wireValue)
    {
        var received = Receive("some/channel", wireValue);

        Assert.Equal("some/channel", received.Topic);
        Assert.Equal(wireValue, received.Payload);
    }

    private static string Golden(string id) => id switch
    {
        WireFormatReferenceMessages.TopicOnly =>
            """{"version":"2.0.0","message":{"topic":"saf/wire/topic-only"}}""",
        WireFormatReferenceMessages.TextPayload =>
            """{"version":"2.0.0","message":{"topic":"saf/wire/text","payload":"{\"value\":42,\"name\":\"sensor\"}"}}""",
        WireFormatReferenceMessages.CustomProperties =>
            """{"version":"2.0.0","message":{"topic":"saf/wire/props","payload":"{\"value\":42}","customProperties":[{"name":"replyTo","value":"saf/wire/reply"},{"name":"correlationId","value":"c3f1a0"}]}}""",
        WireFormatReferenceMessages.EmptyPayload =>
            """{"version":"2.0.0","message":{"topic":"saf/wire/empty","payload":""}}""",
        // The astral emoji is escaped as a surrogate pair because UnicodeRanges.All covers the BMP only,
        // while the BMP characters stay literal. CDE writes the same emoji literally - see the CDE samples.
        WireFormatReferenceMessages.UnicodeAndEscapes =>
            """{"version":"2.0.0","message":{"topic":"saf/wire/unicode","payload":"\"quote\" \\back\\slash / slash\r\n\ttab """
            + NonAsciiLiteral
            + """ \uD83D\uDE00"}}""",
        WireFormatReferenceMessages.NullPropertyValue =>
            """{"version":"2.0.0","message":{"topic":"saf/wire/null-prop","payload":"plain text","customProperties":[{"name":"flag"}]}}""",
        // Optional fields within version 2: older nodes ignore them, the flags travel as a number.
        WireFormatReferenceMessages.ReplyFormats =>
            """{"version":"2.0.0","message":{"topic":"saf/wire/reply-formats","payload":"{\"value\":42}","acceptedReplyFormats":3}}""",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "No recorded sample for this reference message.")
    };

    /// <summary>
    /// The envelope inside the binary frame; the binary payload follows it.
    /// </summary>
    private static string GoldenEnvelope(string id) => id switch
    {
        WireFormatReferenceMessages.Binary =>
            """{"version":"3.0.0","message":{"topic":"saf/wire/binary"}}""",
        WireFormatReferenceMessages.TextAndBinary =>
            """{"version":"3.0.0","message":{"topic":"saf/wire/text-and-binary","payload":"{\"name\":\"chunk\"}","customProperties":[{"name":"index","value":"3"}]}}""",
        WireFormatReferenceMessages.EmptyBinary =>
            """{"version":"3.0.0","message":{"topic":"saf/wire/empty-binary"}}""",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "No recorded sample for this reference message.")
    };

    private static byte[] GoldenFrame(string id)
    {
        var envelope = Encoding.UTF8.GetBytes(GoldenEnvelope(id));
        var envelopeLength = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(envelopeLength, (uint)envelope.Length);
        return [.."SAFB"u8, 1, ..envelopeLength, ..envelope, ..WireFormatReferenceMessages.Create(id).BinaryPayload!];
    }

    private static (string Channel, RedisValue WireValue) Publish(Message message, bool enableBinaryPayloads = true)
    {
        var (messaging, subscriber, _) = CreateMessaging(enableBinaryPayloads);
        string? channel = null;
        var wireValue = RedisValue.Null;
        subscriber.When(s => s.Publish(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>()))
            .Do(ci =>
            {
                channel = ci.ArgAt<RedisChannel>(0).ToString();
                wireValue = ci.ArgAt<RedisValue>(1);
            });

        messaging.Publish(message);

        Assert.NotNull(channel);
        Assert.False(wireValue.IsNull);
        return (channel!, wireValue);
    }

    private static Message Receive(string channel, RedisValue wireValue)
    {
        var (messaging, subscriber, dispatcher) = CreateMessaging();
        Message? received = null;
        dispatcher.When(d => d.DispatchMessage(Arg.Any<Action<Message>>(), Arg.Any<Message>()))
            .Do(ci => received = ci.ArgAt<Message>(1));

        CaptureInternalHandler(messaging, subscriber)(RedisChannel.Literal(channel), wireValue);

        Assert.NotNull(received);
        return received!;
    }

    private static Action<RedisChannel, RedisValue> CaptureInternalHandler(Messaging messaging, ISubscriber subscriber)
    {
        Action<RedisChannel, RedisValue>? internalHandler = null;
        subscriber.When(s => s.Subscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>()))
            .Do(ci => internalHandler = ci.ArgAt<Action<RedisChannel, RedisValue>>(1));

        messaging.Subscribe("*", _ => { });

        Assert.NotNull(internalHandler);
        return internalHandler!;
    }

    private static (Messaging Messaging, ISubscriber Subscriber, IServiceMessageDispatcher Dispatcher) CreateMessaging(
        bool enableBinaryPayloads = true, ILogger<Messaging>? logger = null)
    {
        var dispatcher = Substitute.For<IServiceMessageDispatcher>();
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        multiplexer.GetSubscriber().Returns(subscriber);
        var messaging = new Messaging(logger, multiplexer, dispatcher, TestWireFormat.Writer(enableBinaryPayloads), TestWireFormat.Reader());
        return (messaging, subscriber, dispatcher);
    }

    private static void AssertMessage(Message expected, Message actual)
    {
        Assert.Equal(expected.Topic, actual.Topic);
        Assert.Equal(expected.Payload, actual.Payload);
        Assert.Equal(expected.BinaryPayload, actual.BinaryPayload);
        Assert.Equal(expected.AcceptedReplyFormats, actual.AcceptedReplyFormats);

        if (expected.CustomProperties == null)
        {
            Assert.Null(actual.CustomProperties);
            return;
        }

        Assert.NotNull(actual.CustomProperties);
        Assert.Equal(expected.CustomProperties.Count, actual.CustomProperties!.Count);
        for (var i = 0; i < expected.CustomProperties.Count; i++)
        {
            Assert.Equal(expected.CustomProperties[i].Name, actual.CustomProperties[i].Name);
            Assert.Equal(expected.CustomProperties[i].Value, actual.CustomProperties[i].Value);
        }
    }

    private sealed class LegacyEnvelope
    {
        public string? Version { get; set; }
        public LegacyMessage? Message { get; set; }
    }

    private sealed class LegacyMessage
    {
        public string? Topic { get; set; }
        public string? Payload { get; set; }
        public List<MessageCustomProperty>? CustomProperties { get; set; }
    }
}
