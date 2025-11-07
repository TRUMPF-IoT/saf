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

namespace SAF.Communication.PubSub.Cde.Tests;

public class SubscriptionInternalTests
{
    private readonly ComLine _comLine = Substitute.For<ComLine>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly Subscriber _subscriber;

    public SubscriptionInternalTests()
    {
        _comLine
            .When(m => m.Broadcast(Arg.Is<TSM>(tsm => tsm.TXT == MessageToken.DiscoveryRequest)))
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

    [Fact]
    public void SetRawHandler_AppliesRawHandler()
    {
        var subscription = new SubscriptionInternal(_subscriber);

        var rawHandler = new Action<string, TheProcessMessage>((_, _) => { });
        subscription.SetRawHandler(rawHandler);

        var field = typeof(SubscriptionInternal).GetField("_rawHandler", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var action = Assert.IsType<Action<string, TheProcessMessage>>(field.GetValue(subscription));
        Assert.Equal(rawHandler, action);
    }

    [Fact]
    public void Unsubscribe_ClearsRawHandler()
    {
        var subscription = new SubscriptionInternal(_subscriber);

        var rawHandler = new Action<string, TheProcessMessage>((_, _) => { });
        subscription.SetRawHandler(rawHandler);

        subscription.Unsubscribe();

        var field = typeof(SubscriptionInternal).GetField("_rawHandler", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Null(field.GetValue(subscription));
    }

    [Theory]
    [InlineData(PubSubVersion.V1, "payload")]
    [InlineData(PubSubVersion.V2, "{\"topic\":\"sensor/1\",\"payload\":\"payload\"}")]
    [InlineData(PubSubVersion.V3, "{\"topic\":\"sensor/1\",\"payload\":\"payload\"}")]
    public void OnMessage_NonBatch_InvokesHandlers(string pubSubVersion, string payload)
    {
        var subscription = new SubscriptionInternal(_subscriber, "sensor/*");
        DateTimeOffset? receivedTs = null;
        Message? receivedMsg = null;
        string? rawVersion = null;
        TheProcessMessage? rawMsg = null;

        subscription.SetHandler((ts, m) => { receivedTs = ts; receivedMsg = m; });
        subscription.SetRawHandler((ver, m) => { rawVersion = ver; rawMsg = m; });

        var processMsg = CreateProcessMessage(payload);
        RaiseMessageEvent(_subscriber, "sensor/1", pubSubVersion, processMsg);

        Assert.NotNull(receivedTs);
        Assert.NotNull(receivedMsg);
        Assert.Equal("sensor/1", receivedMsg!.Topic);
        Assert.Equal("payload", receivedMsg.Payload);
        Assert.Equal(pubSubVersion, rawVersion);
        Assert.Same(processMsg, rawMsg);
    }

    [Fact]
    public void OnMessage_Batch_InvokesHandlerForMatchingTopicsOnly()
    {
        var subscription = new SubscriptionInternal(_subscriber, "dev/*");

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
        _ = new SubscriptionInternal(_subscriber, "sensor/*");
        var processMsg = CreateProcessMessage("payload");

        var ex = Record.Exception(() => RaiseMessageEvent(_subscriber, "sensor/3", PubSubVersion.V1, processMsg));
        Assert.Null(ex);
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
        raiseMethod!.DynamicInvoke(topic, version, msg);
    }
}