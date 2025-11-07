// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Reflection;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using NSubstitute;
using SAF.Communication.Cde;
using SAF.Communication.PubSub.Interfaces;
using SAF.Common;
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
    public void OnMessage_NonBatch_InvokesHandlers(string pubSubVersion, string payload)
    {
        var subscription = new Subscription(_subscriber, "sensor/*");
        DateTimeOffset? receivedTs = null;
        Message? receivedMsg = null;

        subscription.SetHandler((ts, m) => { receivedTs = ts; receivedMsg = m; });

        var processMsg = CreateProcessMessage(payload);
        RaiseMessageEvent(_subscriber, "sensor/1", pubSubVersion, processMsg);

        Assert.NotNull(receivedTs);
        Assert.NotNull(receivedMsg);
        Assert.Equal("sensor/1", receivedMsg!.Topic);
        Assert.Equal("payload", receivedMsg.Payload);
    }

    [Fact]
    public void OnMessage_Batch_InvokesHandlerForMatchingTopicsOnly()
    {
        var subscription = new Subscription(_subscriber, "dev/*");

        var handled = new List<string>();
        subscription.SetHandler((_, m) => handled.Add(m.Topic));

        const string batchPayload = "[" + "{\"Topic\":\"dev/A\",\"Payload\":\"A\"}," + "{\"Topic\":\"other/B\",\"Payload\":\"B\"}" + "]";
        var processMsg = CreateProcessMessage(batchPayload);
        RaiseMessageEvent(_subscriber, "$$batch:size=2$$", PubSubVersion.V4, processMsg);

        Assert.Contains("dev/A", handled);
        Assert.DoesNotContain("other/B", handled);
    }

    [Fact]
    public void OnMessage_NoHandlers_EarlyReturn()
    {
        _ = new Subscription(_subscriber, "sensor/*");
        var processMsg = CreateProcessMessage("payload");

        var ex = Record.Exception(() => RaiseMessageEvent(_subscriber, "sensor/3", PubSubVersion.V1, processMsg));
        Assert.Null(ex);
    }

    [Fact]
    public void Unsubscribe_RemovesHandlerAndDetachesFromSubscriber()
    {
        var subscription = _subscriber.Subscribe("sensor/*");

        var invokeCount = 0;
        subscription.SetHandler((_, _) => invokeCount++);

        var processMsg = CreateProcessMessage("payload");
        RaiseMessageEvent(_subscriber, "sensor/1", PubSubVersion.V1, processMsg);
        Assert.Equal(1, invokeCount);

        subscription.Unsubscribe();

        // raising again should not invoke handler
        RaiseMessageEvent(_subscriber, "sensor/1", PubSubVersion.V1, processMsg);
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

        var processMsg = CreateProcessMessage("payload");
        RaiseMessageEvent(_subscriber, "sensor/1", PubSubVersion.V1, processMsg);
        Assert.Equal(1, invokeCount);

        subscription.Dispose();

        // no further invocations
        RaiseMessageEvent(_subscriber, "sensor/1", PubSubVersion.V1, processMsg);
        Assert.Equal(1, invokeCount);

        // subscription removed from internal dictionary
        var subsDictField = typeof(Subscriber).GetField("_subscribers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var dict = (ConcurrentDictionary<Guid, ISubscription>)subsDictField.GetValue(_subscriber)!;
        Assert.False(dict.ContainsKey(subscription.Id));
    }

    private static TheProcessMessage CreateProcessMessage(string payload, DateTimeOffset? tim = null)
    {
        var tsm = new TSM(Engines.PubSub, MessageToken.Publish, payload) { TIM = tim ?? DateTimeOffset.UtcNow };
        return new TheProcessMessage(tsm);
    }

    private static void RaiseMessageEvent(Subscriber subscriber, string topic, string version, TheProcessMessage msg)
    {
        var evtField = typeof(Subscriber).GetField("MessageEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var raiseMethod = evtField.GetValue(subscriber) as MulticastDelegate;
        raiseMethod?.DynamicInvoke(topic, version, msg);
    }
}