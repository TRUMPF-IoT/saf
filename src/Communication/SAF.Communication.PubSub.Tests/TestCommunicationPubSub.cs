// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Tests;
using NSubstitute;
using Common;
using Interfaces;
using Xunit;

public class TestCommunicationPubSub
{
    [Fact]
    public void RunTopic()
    {
        Topic topi = new("channel", "msgId", "version");
        Assert.Equal("channel", topi.Channel);
        Assert.Equal("msgId", topi.MsgId);
        Assert.Equal("version", topi.Version);
        Assert.Equal("channel|msgId|version", topi.ToTsmTxt());
        var topi2 = "channel|msgId|version".ToTopic()!;
        Assert.Equal("channel", topi2.Channel);
        Assert.Equal("msgId", topi2.MsgId);
        Assert.Equal("version", topi2.Version);

        topi2 = "channel|msgId".ToTopic()!;
        Assert.Equal("channel", topi2.Channel);
        Assert.Equal("msgId", topi2.MsgId);
        Assert.Equal("1.0.0", topi2.Version);

        topi2 = "channel".ToTopic();
        Assert.Null(topi2);
        topi2 = "channel|a|b|c".ToTopic();
        Assert.NotNull(topi2);

        topi.Version = string.Empty;
        Assert.Equal("channel|msgId", topi.ToTsmTxt());

        // Sind die folgenden so OK?
        topi.MsgId = string.Empty;
        Assert.Equal("channel|", topi.ToTsmTxt());
        topi.Channel = string.Empty;
        Assert.Equal("|", topi.ToTsmTxt());
    }

    [Fact]
    public void RunWildCardMatcher()
    {
        Assert.True("channel".IsMatch("*"));
        Assert.True("channel".IsMatch("cha*"));
        Assert.False("channel".IsMatch("chab*"));
        Assert.True("x".IsMatch("?"));
        Assert.True("xb".IsMatch("x?"));
        Assert.False("".IsMatch("?"));
        Assert.False("xy".IsMatch("?"));
    }

    [Fact]
    public void RunRegistryLifetimeHandlerBase()
    {
        Topic topi = new("channel", "msgId", "version");
        TestRegistryLifetimeHandlerBase lifetimeHandler = new();
        Assert.Empty(lifetimeHandler.Registries);
        Assert.False(lifetimeHandler.UpdateEventFired);
        lifetimeHandler.HandleMessageDiscoveryResponse(topi);
        Assert.Single(lifetimeHandler.Registries);
        Assert.True(lifetimeHandler.UpdateEventFired);
        Assert.False(lifetimeHandler.DownEventFired);

        lifetimeHandler.UpdateEventFired = false;
        lifetimeHandler.HandleMessageUnsubscribe(topi);
        Assert.Empty(lifetimeHandler.Registries);
        Assert.False(lifetimeHandler.UpdateEventFired);
        Assert.True(lifetimeHandler.DownEventFired);

        lifetimeHandler.DownEventFired = false;
        lifetimeHandler.HandleMessageDiscoveryResponse(topi);
        Assert.True(lifetimeHandler.UpdateEventFired);
        Assert.False(lifetimeHandler.DownEventFired);
        lifetimeHandler.UpdateEventFired = false;
        Assert.Single(lifetimeHandler.Registries);
        Thread.Sleep(500);
        Assert.Single(lifetimeHandler.Registries);
        Thread.Sleep(4000);
        Assert.False(lifetimeHandler.UpdateEventFired);
        Assert.True(lifetimeHandler.DownEventFired);
        Assert.Empty(lifetimeHandler.Registries);
    }

    [Fact]
    public void RunAbstractSubscription()
    {
        var mockSubscriber = Substitute.For<ISubscriber>();
        TestAbstractSubscription testSubscription = new(mockSubscriber, "aPatt*");
        testSubscription.SetHandler((_, topic) => testSubscription.EventFired = true);
        Assert.Single(testSubscription.Patterns);
        Assert.NotEqual("{00000000-0000-0000-0000-000000000000}", testSubscription.Id.ToString());
        Assert.Equal(RoutingOptions.All, testSubscription.RoutingOptions);
        Assert.True(testSubscription.TestMatch("aPattern"));
        Assert.True(testSubscription.TestMatch("aPatt"));
        Assert.False(testSubscription.TestMatch("bPatt"));
        Assert.False(testSubscription.EventFired);
        testSubscription.InvokeHandler();
        Assert.True(testSubscription.EventFired);
        testSubscription.Unsubscribe();
        testSubscription.EventFired = false;
        testSubscription.InvokeHandler();
        Assert.False(testSubscription.EventFired);
    }

}

internal class TestRegistryLifetimeHandlerBase : RegistryLifetimeHandlerBase<Topic>
{
    public bool UpdateEventFired = false;
    public bool DownEventFired = false;

    public TestRegistryLifetimeHandlerBase() : base(1)
    {
        RegistryUp += OnRegistryUp;
        RegistryDown += OnRegistryDown;
    }

    public void HandleMessageDiscoveryResponse(Topic msg)
    {
        HandleMessage(msg.Channel, msg.MsgId, MessageToken.DiscoveryResponse, msg);
    }

    public void HandleMessageUnsubscribe(Topic msg)
    {
        HandleMessage(msg.Channel, msg.MsgId, MessageToken.RegistryShutdown, msg);
    }

    private void OnRegistryUp(Topic registry, string reasonToken)
    {
        UpdateEventFired = true;
    }

    private void OnRegistryDown(Topic registry)
    {
        DownEventFired = true;
    }
}

internal class TestAbstractSubscription : AbstractSubscription
{
    public bool EventFired = false;

    private readonly ISubscriber _subscriber;

    public TestAbstractSubscription(ISubscriber subscriber, params string[] patterns)
        : this(subscriber, RoutingOptions.All, patterns)
    { }

    public TestAbstractSubscription(ISubscriber subscriber, RoutingOptions routingOptions, params string[] patterns) : base(subscriber, routingOptions, patterns)
    {
        _subscriber = subscriber;
    }

    public void InvokeHandler()
    {
        Handler?.Invoke(new DateTimeOffset(DateTime.Now), new Message());
    }

    public bool TestMatch(string topic)
    {
        return IsTopicMatch(topic);
    }

}