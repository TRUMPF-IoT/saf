// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using NSubstitute;
using SAF.Messaging.Contracts;
using StackExchange.Redis;
using TestUtilities.WireFormat;
using Xunit;

/// <summary>
/// Records the Redis wire format as an old SAF node sees it. Verified to be identical in 9.0.1,
/// 10.0.3 and 11.0.0-alpha.9, so these samples cover every version SAF has to interoperate with.
/// A failure here means the wire format changed - that is only allowed together with a deliberate
/// bump of <see cref="RedisMessageVersion"/> and a reader for the old version.
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

    [Theory]
    [MemberData(nameof(ReferenceIds))]
    public void Publish_ProducesRecordedWireFormat(string id)
    {
        var (channel, wireValue) = Publish(WireFormatReferenceMessages.Create(id));

        Assert.Equal(WireFormatReferenceMessages.Create(id).Topic, channel);
        Assert.Equal(Golden(id), wireValue);
    }

    [Theory]
    [MemberData(nameof(ReferenceIds))]
    public void Subscribe_ReadsRecordedWireFormat(string id)
    {
        var expected = WireFormatReferenceMessages.Create(id);

        var received = Receive(expected.Topic, Golden(id));

        AssertMessage(expected, received);
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
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "No recorded sample for this reference message.")
    };

    private static (string Channel, string WireValue) Publish(Message message)
    {
        var (messaging, subscriber, _) = CreateMessaging();
        string? channel = null;
        string? wireValue = null;
        subscriber.When(s => s.Publish(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>()))
            .Do(ci =>
            {
                channel = ci.ArgAt<RedisChannel>(0).ToString();
                wireValue = ci.ArgAt<RedisValue>(1).ToString();
            });

        messaging.Publish(message);

        Assert.NotNull(channel);
        Assert.NotNull(wireValue);
        return (channel!, wireValue!);
    }

    private static Message Receive(string channel, string wireValue)
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

    private static (Messaging Messaging, ISubscriber Subscriber, IServiceMessageDispatcher Dispatcher) CreateMessaging()
    {
        var dispatcher = Substitute.For<IServiceMessageDispatcher>();
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        multiplexer.GetSubscriber().Returns(subscriber);
        return (new Messaging(null, multiplexer, dispatcher, TestWireFormat.Writer(), TestWireFormat.Reader()), subscriber, dispatcher);
    }

    private static void AssertMessage(Message expected, Message actual)
    {
        Assert.Equal(expected.Topic, actual.Topic);
        Assert.Equal(expected.Payload, actual.Payload);

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
}
