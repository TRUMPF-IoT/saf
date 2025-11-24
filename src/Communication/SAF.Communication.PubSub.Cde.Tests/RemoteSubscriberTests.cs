// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests;

using Common;
using Communication.Cde;
using Interfaces;
using nsCDEngine.BaseClasses;
using NSubstitute;
using SAF.Communication.PubSub.Cde.MessageProcessing;
using System.Reflection;
using Xunit;

public class RemoteSubscriberTests
{
    private readonly ComLine _line = Substitute.For<ComLine>();

    public RemoteSubscriberTests()
    {
        _line.Address.Returns("origin");
    }

    [Fact]
    public void Broadcast_Skips_WhenRoutingNotAllowed()
    {
        var rs = Create(PubSubVersion.V1, ["sensor/*"], localHost: false); // remote host, Local routing will be disallowed
        
        var bm = new BroadcastMessage(new Topic("sensor/1", "id", PubSubVersion.V1), new Message { Topic = "sensor/1", Payload = "p" }, "user", RoutingOptions.Local);
        rs.Broadcast(bm);

        _line.DidNotReceive().AnswerToSender(Arg.Any<TSM>(), Arg.Any<TSM>());
    }

    [Fact]
    public void Broadcast_Skips_WhenTopicNotMatched()
    {
        var rs = Create(PubSubVersion.V1, ["other/*"], localHost: true);
        
        var bm = CreateBroadcastMessage(channel: "sensor/1", version: PubSubVersion.V1);
        rs.Broadcast(bm);

        _line.DidNotReceive().AnswerToSender(Arg.Any<TSM>(), Arg.Any<TSM>());
    }

    [Fact]
    public void Broadcast_V1_SendsImmediateNonBatch()
    {
        var rs = Create(PubSubVersion.V1, ["sensor/*"], localHost: true);
        
        var bm = CreateBroadcastMessage(channel: "sensor/99", payload: "pl99", version: PubSubVersion.V1);
        rs.Broadcast(bm);

        _line.Received(1).AnswerToSender(rs.Tsm,
            Arg.Is<TSM>(t =>
                t.TXT.StartsWith($"{MessageToken.Publish}:sensor/99") &&
                t.TXT.EndsWith(PubSubVersion.V1) &&
                t.PLS == "pl99" &&
                t.ENG == Engines.RemotePubSub));
    }

    [Theory]
    [InlineData(PubSubVersion.V2)]
    [InlineData(PubSubVersion.V3)]
    public void Broadcast_V2V3_SendsImmediateNonBatch(string pubSubVersion)
    {
        var rs = Create(pubSubVersion, ["sensor/*"], localHost: true);

        var bm = CreateBroadcastMessage(channel: "sensor/99", payload: "pl99", version: pubSubVersion);
        rs.Broadcast(bm);

        _line.Received(1).AnswerToSender(rs.Tsm,
            Arg.Is<TSM>(t =>
                t.TXT.StartsWith($"{MessageToken.Publish}:sensor/99") &&
                t.TXT.EndsWith(pubSubVersion) &&
                t.PLS == "{\"Topic\":\"sensor/99\",\"Payload\":\"pl99\"}" &&
                t.ENG == Engines.RemotePubSub));
    }

    [Fact]
    public async Task Broadcast_V4_QueuesForBatchProcessing()
    {
        var processedEvent = new TaskCompletionSource<bool>();

        _line.When(m =>
                m.AnswerToSender(Arg.Any<TSM>(), Arg.Is<TSM>(t => t.TXT.StartsWith(MessageToken.Publish) && t.TXT.Contains("$$batch"))))
            .Do(_ => processedEvent.TrySetResult(true));

        var rs = Create(PubSubVersion.V4, ["sensor/*"], localHost: true);

        var bm = CreateBroadcastMessage(channel: "sensor/1");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var tokenReg = cts.Token.Register(() => processedEvent.TrySetResult(false));

        rs.Broadcast(bm);

        var eventProcessed = await processedEvent.Task;
        Assert.True(eventProcessed);
        _line.Received().AnswerToSender(rs.Tsm,
            Arg.Is<TSM>(t => t.TXT.StartsWith(MessageToken.Publish) && t.TXT.Contains("$$batch")));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 1)]
    [InlineData(101, 2)]
    [InlineData(199, 2)]
    [InlineData(200, 2)]
    [InlineData(201, 3)]
    public void CreateMessageBlocks_ConsidersMaxMessagesPerBlock(int messageCount, int expectedBlocks)
    {
        var messages = Enumerable.Range(0, messageCount).Select(i => new Message { Topic = $"sensor/{i}", Payload = "pl" + i }).ToList();
        var messageBlocks = CallCreateMessageBlocks(messages).ToList();

        Assert.Equal(expectedBlocks, messageBlocks.Count);
    }

    [Theory]
    [InlineData(1, 1024, 1)]
    [InlineData(1, 200 * 1024, 1)]
    [InlineData(1, 200 * 1024 + 1, 1)]
    [InlineData(2, 100 * 1024 + 1, 2)]
    [InlineData(4, 100 * 1024, 2)]
    public void CreateMessageBlocks_ConsidersMaxPayloadBytesPerBlock(int messageCount, int payloadSize, int expectedBlocks)
    {
        var messages = Enumerable.Range(0, messageCount)
            .Select(i => new Message { Topic = $"sensor/{i}", Payload = new string(Enumerable.Range(0, payloadSize).Select(_ => '0').ToArray()) }).ToList();
        var messageBlocks = CallCreateMessageBlocks(messages).ToList();

        Assert.Equal(expectedBlocks, messageBlocks.Count);
    }

    private RemoteSubscriber Create(string version, IList<string>? patterns = null, bool localHost = true)
    {
        var tsm = new TSM(Engines.PubSub, MessageToken.SubscribeRequest) { ORG = localHost ? _line.Address : "remote" };
        var req = new RegistrySubscriptionRequest { version = version, isRegistry = false };
        return new RemoteSubscriber(_line, tsm, patterns ?? ["sensor/*"], req);
    }

    private static BroadcastMessage CreateBroadcastMessage(string channel = "sensor/1", string payload = "data", string userId = "user", string version = PubSubVersion.V4)
    {
        var topic = new Topic(channel, Guid.NewGuid().ToString("N"), version);
        var msg = new Message { Topic = channel, Payload = payload };
        return new BroadcastMessage(topic, msg, userId, RoutingOptions.All);
    }

    private static IEnumerable<IReadOnlyList<Message>> CallCreateMessageBlocks(IEnumerable<Message> messages)
    {
        var method = typeof(RemoteSubscriber).GetMethod("CreateMessageBlocks", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (IEnumerable<IReadOnlyList<Message>>)method.Invoke(null, [messages])!;
    }
}