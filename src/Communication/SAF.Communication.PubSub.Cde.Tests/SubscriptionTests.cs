// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using NSubstitute;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Interfaces;
using SAF.Messaging.Contracts;
using Xunit;
using System.Collections.Concurrent;

namespace SAF.Communication.PubSub.Cde.Tests;

public class SubscriptionTests
{
    private readonly ComLine _comLine = Substitute.For<ComLine>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly Subscriber _subscriber;

    public SubscriptionTests()
    {
        _comLine
            .When(m => m.Broadcast(Arg.Is<TSM>(tsm => tsm.TXT.StartsWith(MessageToken.DiscoveryRequest))))
            .Do(_ =>
            { 
                var discoveryTsm = new TSM(Engines.PubSub, MessageToken.DiscoveryResponse, "{\"address\":\"test-address\",\"instanceId\":\"fast\",\"version\":\"4.0.0\"}")
                {
                    ORG = _comLine.Address
                };
                _comLine.MessageReceived += Raise.Event<MessageReceivedHandler>(Substitute.For<ICDEThing>(), new TheProcessMessage(discoveryTsm));
            });

        _subscriber = new Subscriber(_comLine, _publisher, CancellationToken.None);
    }

    [Theory]
    [InlineData(PubSubVersion.V1, "payload")]
    [InlineData(PubSubVersion.V2, "{\"topic\":\"sensor/1\",\"payload\":\"payload\"}")]
    [InlineData(PubSubVersion.V3, "{\"topic\":\"sensor/1\",\"payload\":\"payload\"}")]
    [InlineData(PubSubVersion.V4, "{\"topic\":\"sensor/1\",\"payload\":\"payload\"}")]
    public void OnMessage_NonBatch_InvokesHandlers(string pubSubVersion, string payload)
    {
        var subscription = new Subscription(_subscriber, "sensor/*");
        DateTimeOffset? receivedTs = null;
        Message? receivedMsg = null;

        subscription.SetHandler((ts, m) => { receivedTs = ts; receivedMsg = m; });

        RaisePublication("sensor/1", pubSubVersion, payload);

        Assert.NotNull(receivedTs);
        Assert.NotNull(receivedMsg);
        Assert.Equal("sensor/1", receivedMsg!.Topic);
        Assert.Equal("payload", receivedMsg.Payload);
    }

    [Fact]
    public void OnMessage_NonBatch_IgnoresUnmatchedPayload()
    {
        var subscription = new Subscription(_subscriber, "sensor/*");
        var invokeCount = 0;
        subscription.SetHandler((_, _) => invokeCount++);

        var ex = Record.Exception(() => RaisePublication("other/1", PubSubVersion.V4, "not JSON"));

        Assert.Null(ex);
        Assert.Equal(0, invokeCount);
    }

    [Fact]
    public void OnMessage_Batch_InvokesHandlerForMatchingTopicsOnly()
    {
        var subscription = new Subscription(_subscriber, "dev/*");

        var handled = new List<string>();
        subscription.SetHandler((_, m) => handled.Add(m.Topic));

        const string batchPayload = "[" + "{\"Topic\":\"dev/A\",\"Payload\":\"A\"}," + "{\"Topic\":\"other/B\",\"Payload\":\"B\"}" + "]";
        RaisePublication("$$batch:size=2$$", PubSubVersion.V4, batchPayload);

        Assert.Contains("dev/A", handled);
        Assert.DoesNotContain("other/B", handled);
    }

    [Fact]
    public void OnMessage_Batch_DispatchesSameParsedBatchToAllSubscriptions()
    {
        var subscription = new Subscription(_subscriber, "dev/*");
        var handled = new List<string>();
        subscription.SetHandler((_, message) => handled.Add(message.Topic));

        var batches = new List<IReadOnlyList<Message>?>();
        _subscriber.MessageEvent += (_, _, _, batch) => batches.Add(batch);
        _subscriber.MessageEvent += (_, _, _, batch) => batches.Add(batch);

        RaisePublication("$$batch:size=1$$", PubSubVersion.V4, "[{\"Topic\":\"dev/A\",\"Payload\":\"A\"}]");

        Assert.Equal(["dev/A"], handled);
        Assert.Equal(2, batches.Count);
        Assert.NotNull(batches[0]);
        Assert.Same(batches[0], batches[1]);
    }

    [Fact]
    public void OnMessage_Batch_KeepsHandlerMessagesIndependent()
    {
        var first = new Subscription(_subscriber, "dev/*");
        var second = new Subscription(_subscriber, "dev/A");
        Message? firstMessage = null;
        Message? secondMessage = null;

        first.SetHandler((_, message) =>
        {
            firstMessage = message;
            message.Topic = "changed";
            message.Payload = "changed";
            message.CustomProperties![0].Value = "changed";
        });
        second.SetHandler((_, message) => secondMessage = message);

        RaisePublication("$$batch:size=1$$", PubSubVersion.V4,
            "[{\"Topic\":\"dev/A\",\"Payload\":\"A\",\"CustomProperties\":[{\"Name\":\"source\",\"Value\":\"original\"}]}]");

        Assert.NotNull(firstMessage);
        Assert.NotNull(secondMessage);
        Assert.NotSame(firstMessage, secondMessage);
        Assert.Equal("dev/A", secondMessage!.Topic);
        Assert.Equal("A", secondMessage.Payload);
        Assert.Equal("original", Assert.Single(secondMessage.CustomProperties!).Value);
    }

    [Fact]
    public void OnMessage_NoHandlers_EarlyReturn()
    {
        _ = new Subscription(_subscriber, "sensor/*");

        var ex = Record.Exception(() => RaisePublication("sensor/3", PubSubVersion.V1, "payload"));
        Assert.Null(ex);
    }

    [Fact]
    public void Unsubscribe_RemovesHandlerAndDetachesFromSubscriber()
    {
        var subscription = _subscriber.Subscribe("sensor/*");

        var invokeCount = 0;
        subscription.SetHandler((_, _) => invokeCount++);

        RaisePublication("sensor/1", PubSubVersion.V1, "payload");
        Assert.Equal(1, invokeCount);

        subscription.Unsubscribe();

        // raising again should not invoke handler
        RaisePublication("sensor/1", PubSubVersion.V1, "payload");
        Assert.Equal(1, invokeCount); // unchanged

        // assert subscription removed from subscriber registry
        var subsDictField = typeof(Subscriber).GetField("_subscribers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var dict = (ConcurrentDictionary<Guid, ISubscription>)subsDictField.GetValue(_subscriber)!;
        Assert.False(dict.ContainsKey(subscription.Id));
    }

    [Fact]
    public void Dispose_CallsUnsubscribe()
    {
        var subscription = _subscriber.Subscribe("sensor/*");

        var invokeCount = 0;
        subscription.SetHandler((_, _) => invokeCount++);

        RaisePublication("sensor/1", PubSubVersion.V1, "payload");
        Assert.Equal(1, invokeCount);

        subscription.Dispose();

        // no further invocations
        RaisePublication("sensor/1", PubSubVersion.V1, "payload");
        Assert.Equal(1, invokeCount);

        // subscription removed from internal dictionary
        var subsDictField = typeof(Subscriber).GetField("_subscribers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var dict = (ConcurrentDictionary<Guid, ISubscription>)subsDictField.GetValue(_subscriber)!;
        Assert.False(dict.ContainsKey(subscription.Id));
    }

    private void RaisePublication(string topic, string version, string payload)
    {
        var messageTxt = $"{MessageToken.Publish}:{new Topic(topic, Guid.NewGuid().ToString("N"), version).ToTsmTxt()}";
        var tsm = new TSM(Engines.PubSub, messageTxt, payload) { TIM = DateTimeOffset.UtcNow };
        _comLine.MessageReceived += Raise.Event<MessageReceivedHandler>(Substitute.For<ICDEThing>(), new TheProcessMessage(tsm));
    }
}

