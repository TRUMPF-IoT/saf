// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests.WireFormat;

using System.Reflection;
using Communication.Cde;
using Interfaces;
using nsCDEngine.BaseClasses;
using NSubstitute;
using SAF.Communication.PubSub.Cde.MessageProcessing;
using SAF.Messaging.Contracts;
using TestUtilities.WireFormat;
using Xunit;

/// <summary>
/// Records the C-DEngine wire format per negotiated peer version, as an old SAF node sees it.
/// V3 matters in practice: a 9.x node announces <see cref="PubSubVersion.V3"/>, while 10.x and 11.x
/// announce <see cref="PubSubVersion.V4"/>. A failure here means an old peer would read something
/// different than it does today, which is only allowed together with a new <see cref="PubSubVersion"/>.
/// </summary>
public class CdeWireFormatGoldenTests
{
    private const string MsgId = "00000000000000000000000000000001";

    [Theory]
    [InlineData(WireFormatReferenceMessages.TopicOnly, "saf/wire/topic-only")]
    [InlineData(WireFormatReferenceMessages.TextPayload, "saf/wire/text")]
    [InlineData(WireFormatReferenceMessages.CustomProperties, "saf/wire/props")]
    [InlineData(WireFormatReferenceMessages.EmptyPayload, "saf/wire/empty")]
    [InlineData(WireFormatReferenceMessages.UnicodeAndEscapes, "saf/wire/unicode")]
    [InlineData(WireFormatReferenceMessages.NullPropertyValue, "saf/wire/null-prop")]
    public async Task Broadcast_V1_SendsPayloadOnly(string id, string topic)
    {
        var message = WireFormatReferenceMessages.Create(id);

        var tsm = await BroadcastAsync(PubSubVersion.V1, message);

        Assert.Equal($"{MessageToken.Publish}:{topic}|{MsgId}|{PubSubVersion.V1}", tsm.TXT);
        Assert.Equal(message.Payload, tsm.PLS);
    }

    [Theory]
    [InlineData(PubSubVersion.V2)]
    [InlineData(PubSubVersion.V3)]
    public async Task Broadcast_V2AndV3_SendTheWholeMessageAsPascalCaseJson(string version)
    {
        foreach (var id in WireFormatReferenceMessages.Ids)
        {
            var message = WireFormatReferenceMessages.Create(id);

            var tsm = await BroadcastAsync(version, message);

            Assert.Equal($"{MessageToken.Publish}:{message.Topic}|{MsgId}|{version}", tsm.TXT);
            Assert.Equal(GoldenJson(id), tsm.PLS);
        }
    }

    [Theory]
    [InlineData(WireFormatReferenceMessages.TopicOnly)]
    [InlineData(WireFormatReferenceMessages.TextPayload)]
    [InlineData(WireFormatReferenceMessages.CustomProperties)]
    [InlineData(WireFormatReferenceMessages.EmptyPayload)]
    [InlineData(WireFormatReferenceMessages.UnicodeAndEscapes)]
    [InlineData(WireFormatReferenceMessages.NullPropertyValue)]
    public async Task Broadcast_V4_SendsABatchArray(string id)
    {
        var message = WireFormatReferenceMessages.Create(id);

        var tsm = await BroadcastAsync(PubSubVersion.V4, message);

        // The batch message id is generated per block and cannot be pinned, the rest of TXT can.
        Assert.StartsWith($"{MessageToken.Publish}:$$batch:size=1$$|", tsm.TXT);
        Assert.EndsWith($"|{PubSubVersion.V4}", tsm.TXT);
        Assert.Equal($"[{GoldenJson(id)}]", tsm.PLS);
    }

    [Theory]
    [InlineData(WireFormatReferenceMessages.TopicOnly)]
    [InlineData(WireFormatReferenceMessages.TextPayload)]
    [InlineData(WireFormatReferenceMessages.CustomProperties)]
    [InlineData(WireFormatReferenceMessages.EmptyPayload)]
    [InlineData(WireFormatReferenceMessages.UnicodeAndEscapes)]
    [InlineData(WireFormatReferenceMessages.NullPropertyValue)]
    public void ExtractMessagesFromTsm_ReadsV2AndV3Samples(string id)
    {
        var expected = WireFormatReferenceMessages.Create(id);

        foreach (var version in new[] { PubSubVersion.V2, PubSubVersion.V3 })
        {
            var messages = ExtractMessagesFromTsm(expected.Topic, version, GoldenJson(id));

            AssertSingle(expected, messages);
        }
    }

    [Theory]
    [InlineData(WireFormatReferenceMessages.TopicOnly)]
    [InlineData(WireFormatReferenceMessages.TextPayload)]
    [InlineData(WireFormatReferenceMessages.CustomProperties)]
    [InlineData(WireFormatReferenceMessages.EmptyPayload)]
    [InlineData(WireFormatReferenceMessages.UnicodeAndEscapes)]
    [InlineData(WireFormatReferenceMessages.NullPropertyValue)]
    public void ExtractMessagesFromTsm_ReadsV4BatchSamples(string id)
    {
        var expected = WireFormatReferenceMessages.Create(id);

        var messages = ExtractMessagesFromTsm("$$batch:size=1$$", PubSubVersion.V4, $"[{GoldenJson(id)}]");

        AssertSingle(expected, messages);
    }

    [Fact]
    public void ExtractMessagesFromTsm_ReadsV1AsPayloadOnly()
    {
        var messages = ExtractMessagesFromTsm("saf/wire/text", PubSubVersion.V1, "just the payload");

        Assert.Single(messages);
        Assert.Equal("saf/wire/text", messages[0].Topic);
        Assert.Equal("just the payload", messages[0].Payload);
    }

    /// <summary>
    /// A non-batch message is still read as a single message on V4, which is what a 9.x or 10.x
    /// sender produces when it talks to a V4 peer without batching.
    /// </summary>
    [Fact]
    public void ExtractMessagesFromTsm_ReadsNonBatchV4AsSingleMessage()
    {
        var expected = WireFormatReferenceMessages.Create(WireFormatReferenceMessages.TextPayload);

        var messages = ExtractMessagesFromTsm(expected.Topic, PubSubVersion.V4,
            GoldenJson(WireFormatReferenceMessages.TextPayload));

        AssertSingle(expected, messages);
    }

    /// <summary>
    /// The recorded JSON of the whole message as C-DEngine's serializer writes it: PascalCase, nulls omitted,
    /// every non-ASCII character literal - including astral ones, where SAF's own JSON serializer escapes
    /// them as a surrogate pair. The two serializers are not interchangeable.
    /// </summary>
    private static string GoldenJson(string id) => id switch
    {
        WireFormatReferenceMessages.TopicOnly =>
            """{"Topic":"saf/wire/topic-only"}""",
        WireFormatReferenceMessages.TextPayload =>
            """{"Topic":"saf/wire/text","Payload":"{\"value\":42,\"name\":\"sensor\"}"}""",
        WireFormatReferenceMessages.CustomProperties =>
            """{"Topic":"saf/wire/props","Payload":"{\"value\":42}","CustomProperties":[{"Name":"replyTo","Value":"saf/wire/reply"},{"Name":"correlationId","Value":"c3f1a0"}]}""",
        WireFormatReferenceMessages.EmptyPayload =>
            """{"Topic":"saf/wire/empty","Payload":""}""",
        WireFormatReferenceMessages.UnicodeAndEscapes =>
            """{"Topic":"saf/wire/unicode","Payload":"\"quote\" \\back\\slash / slash\r\n\ttab äöüß 日本語 😀"}""",
        WireFormatReferenceMessages.NullPropertyValue =>
            """{"Topic":"saf/wire/null-prop","Payload":"plain text","CustomProperties":[{"Name":"flag"}]}""",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "No recorded sample for this reference message.")
    };

    private static async Task<TSM> BroadcastAsync(string version, Message message)
    {
        var captured = new TaskCompletionSource<TSM>();
        var line = Substitute.For<ComLine>();
        line.Address.Returns("origin");
        line.When(l => l.AnswerToSender(Arg.Any<TSM>(), Arg.Any<TSM>()))
            .Do(ci => captured.TrySetResult(ci.ArgAt<TSM>(1)));

        var subscriberTsm = new TSM(Engines.PubSub, MessageToken.SubscribeRequest) { ORG = line.Address };
        var request = new RegistrySubscriptionRequest { version = version, isRegistry = false };
        var remote = new RemoteSubscriber(line, subscriberTsm, ["*"], request);

        remote.Broadcast(new BroadcastMessage(new Topic(message.Topic, MsgId, version), message, "user", RoutingOptions.All));

        return await captured.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static List<Message> ExtractMessagesFromTsm(string channel, string version, string pls)
    {
        var method = typeof(SubscriptionRegistry)
            .GetMethod("ExtractMessagesFromTsm", BindingFlags.Static | BindingFlags.NonPublic)!;
        var topic = new Topic(channel, MsgId, version);
        var tsm = new TSM(Engines.PubSub, $"{MessageToken.Publish}:{topic.ToTsmTxt()}", pls);

        return (List<Message>)method.Invoke(null, [topic, tsm])!;
    }

    private static void AssertSingle(Message expected, List<Message> actual)
    {
        Assert.Single(actual);
        Assert.Equal(expected.Topic, actual[0].Topic);
        Assert.Equal(expected.Payload, actual[0].Payload);

        if (expected.CustomProperties == null)
        {
            Assert.Null(actual[0].CustomProperties);
            return;
        }

        Assert.NotNull(actual[0].CustomProperties);
        Assert.Equal(expected.CustomProperties.Count, actual[0].CustomProperties!.Count);
        for (var i = 0; i < expected.CustomProperties.Count; i++)
        {
            Assert.Equal(expected.CustomProperties[i].Name, actual[0].CustomProperties![i].Name);
            Assert.Equal(expected.CustomProperties[i].Value, actual[0].CustomProperties![i].Value);
        }
    }
}
