// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests;
using NSubstitute;
using Xunit;
using Common;
using SAF.Communication.Cde;
using Interfaces;
using nsCDEngine.ViewModels;
using nsCDEngine.BaseClasses;

public class TestCommunicationPubSubCde
{
    [Fact]
    public void RunRegistryIdentity()
    {
        RegistryIdentity ri = new("adress", "instanceID");
        Assert.Equal("adress", ri.address);
        Assert.Equal("instanceID", ri.instanceId);
    }

    [Fact]
    public void RunRemoteRegistryLifetimeHandler()
    {
        RemoteRegistryLifetimeHandler rrlh = new();
        Assert.Empty(rrlh.Registries);

        CheckRegistry(rrlh, MessageToken.RegistryAlive);
        CheckRegistryShutdown(rrlh);

        CheckRegistry(rrlh, MessageToken.SubscriberAlive);
        CheckRegistryShutdown(rrlh);

        CheckRegistry(rrlh, MessageToken.DiscoveryResponse);
        CheckRegistryShutdown(rrlh);

        CheckRegistry(rrlh, MessageToken.SubscribeTrigger);
        CheckRegistryShutdown(rrlh);

        RegistrySubscriptionResponse rsr = new()
        {
            id = "id",
            instanceId = "instanceID"
        };
        TSM tsm = new(Engines.PubSub, MessageToken.SubscribeResponse, TheCommonUtils.SerializeObjectToJSONString<RegistrySubscriptionResponse>(rsr));
        TheProcessMessage tpm = new(tsm);
        rrlh.HandleMessage(tpm);
        Assert.Single(rrlh.Registries);
        Assert.Equal(tsm, rrlh.Registries[0]);
        CheckRegistryShutdown(rrlh);

        CheckNoRegistry(rrlh, MessageToken.DiscoveryRequest);
        CheckNoRegistry(rrlh, MessageToken.Error);
        CheckNoRegistry(rrlh, MessageToken.Publish);
        CheckNoRegistry(rrlh, MessageToken.SubscribeRequest);
        CheckNoRegistry(rrlh, MessageToken.SubscriberShutdown);
        CheckNoRegistry(rrlh, MessageToken.Unsubscribe);
    }

    private static void CheckRegistry(RemoteRegistryLifetimeHandler rrlh, string messageToken)
    {
        TSM tsm = new(Engines.PubSub, messageToken, "{\"address\":\"\",\"instanceId\":\"fee034c582a34e18afc248b82932034d\",\"version\":\"3.0.0\"}");
        TheProcessMessage tpm = new(tsm);
        rrlh.HandleMessage(tpm);
        Assert.Single(rrlh.Registries);
        Assert.Equal(tsm, rrlh.Registries[0]);
    }

    private static void CheckNoRegistry(RemoteRegistryLifetimeHandler rrlh, string messageToken)
    {
        TSM tsm = new(Engines.PubSub, messageToken);
        TheProcessMessage tpm = new(tsm);
        rrlh.HandleMessage(tpm);
        Assert.Empty(rrlh.Registries);
    }

    private static void CheckRegistryShutdown(RemoteRegistryLifetimeHandler rrlh)
    {
        TSM tsm = new(Engines.PubSub, MessageToken.RegistryShutdown);
        TheProcessMessage tpm = new(tsm);
        rrlh.HandleMessage(tpm);
        Assert.Empty(rrlh.Registries);

    }

    [Fact]
    public void RunRemoteSubscriber()
    {
        TSM tsm = new(Engines.PubSub, MessageToken.SubscribeRequest);
        List<string> lstPattern = ["pattern"];
        RegistrySubscriptionRequest rsr = new()
        {
            isRegistry = false
        };
        RemoteSubscriber resu = new(Substitute.For<ComLine>(), tsm, lstPattern, rsr);
        Assert.Equal(tsm, resu.Tsm);
        Assert.True(resu.IsAlive);
        Assert.False(resu.IsRegistry);
        Assert.Equal(Engines.RemotePubSub, resu.TargetEngine);
        Assert.Equal(PubSubVersion.V1, resu.Version);
        rsr.version = PubSubVersion.Latest;
        Assert.Equal(PubSubVersion.Latest, resu.Version);
        Assert.True(resu.HasPatterns);
        resu.RemovePatterns(["pattern"]);
        Assert.False(resu.HasPatterns);
        resu.AddPatterns(["patt*"]);
        Assert.True(resu.HasPatterns);
        Assert.True(resu.IsMatch("pattern"));
        Assert.False(resu.IsMatch("patern"));
        Assert.True(resu.IsRoutingAllowed(RoutingOptions.All));
        Assert.False(resu.IsRoutingAllowed(RoutingOptions.Local));
        Assert.True(resu.IsRoutingAllowed(RoutingOptions.Remote));
    }

    [Fact]
    public void RunPublisherWithSubstitute()
    {
        var comLinePublisher = Substitute.For<ComLine>();
        comLinePublisher.Address.Returns("NOT RUNNING");
        Publisher publisher = new(comLinePublisher);
        var subscriptionRegistry = Substitute.For<ISubscriptionRegistry>();
        publisher._subscriptionRegistry = subscriptionRegistry;

        publisher.Publish("channel", "payload");
        CheckBroadcast(subscriptionRegistry);
        publisher.Publish("channel", "payload", RoutingOptions.Local);
        CheckBroadcast(subscriptionRegistry, RoutingOptions.Local);

        Message msg = new() { Topic = "channel", Payload = "payload" };
        publisher.Publish(msg);
        CheckBroadcast(subscriptionRegistry);
        publisher.Publish(msg, RoutingOptions.Local);
        CheckBroadcast(subscriptionRegistry, RoutingOptions.Local);

        var guid = Guid.NewGuid();
        publisher.Publish(msg, guid);
        CheckBroadcast(subscriptionRegistry, guidString: guid.ToString());
        publisher.Publish(msg, guid, RoutingOptions.Local);
        CheckBroadcast(subscriptionRegistry, RoutingOptions.Local, guid.ToString());

        publisher.Publish(msg, guid.ToString());
        CheckBroadcast(subscriptionRegistry, guidString: guid.ToString());
        publisher.Publish(msg, guid.ToString(), RoutingOptions.Local);
        CheckBroadcast(subscriptionRegistry, RoutingOptions.Local, guid.ToString());
    }

    private static void CheckBroadcast(ISubscriptionRegistry subscriptionRegistry,
        RoutingOptions routingOptions = RoutingOptions.All,
        string guidString = "00000000-0000-0000-0000-000000000000")
    {
        subscriptionRegistry.Received().Broadcast(Arg.Is<Topic>(t => t.Channel.Equals("channel") && t.MsgId.Length == 32),
            Arg.Is<Message>(m => m.Topic.Equals("channel") && m.Payload!.Equals("payload")),
            guidString, routingOptions);
        subscriptionRegistry.ClearReceivedCalls();
    }

    [Fact]
    public void RunSubscriber()
    {
        var comLineSubscriber = Substitute.For<ComLine>();
        var publisher = Substitute.For<IPublisher>();
        comLineSubscriber.Address.Returns("NOT RUNNING");
        TSM? tsmResult = null;
        comLineSubscriber.AnswerToSender(Arg.Any<TSM>(), Arg.Do<TSM>(t => tsmResult = t));

        Subscriber subscriber = new(comLineSubscriber, publisher);
        comLineSubscriber.Received().Broadcast(Arg.Is<TSM>(t => t.ENG.Equals(Engines.PubSub) && t.TXT.Equals(MessageToken.DiscoveryRequest)));

        // The following discovery response simulates the reaction of the
        // discovery request inside the subscriber constructor.
        RegistryIdentity ri = new ("adress", "identID");
        TSM tsm = new(Engines.PubSub, MessageToken.DiscoveryResponse, TheCommonUtils.SerializeObjectToJSONString<RegistryIdentity>(ri));
        TheProcessMessage tpm = new(tsm);
        comLineSubscriber.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpm);
        // The count of registered nodes will be checked by the follofwing subscribe request.

        var subsciptionInternal = subscriber.Subscribe("pattern");
        comLineSubscriber.Received().AnswerToSender(Arg.Any<TSM>(), Arg.Is<TSM>(t => t.ENG.Equals(Engines.PubSub) && t.TXT.Equals(MessageToken.SubscribeRequest)));

        subscriber.Unsubscribe(subsciptionInternal);
        comLineSubscriber.Received().AnswerToSender(Arg.Any<TSM>(), Arg.Is<TSM>(t => t.ENG.Equals(Engines.PubSub) && t.TXT.Equals(MessageToken.Unsubscribe)));
    }

    [Fact]
    public async Task RunSubscriptionRegistryWithSubstitute()
    {
        var comLineSubscriptionRegistry = Substitute.For<ComLine>();
        comLineSubscriptionRegistry.Address.Returns("NOT RUNNING");
        SubscriptionRegistry subscriptionRegistry = new(comLineSubscriptionRegistry);
        await subscriptionRegistry.ConnectAsync(new CancellationTokenSource().Token);

        TSM? tsmResult = null;
        comLineSubscriptionRegistry.AnswerToSender(Arg.Any<TSM>(), Arg.Do<TSM>(t => tsmResult = t));

        // Sende a discovery request and receive a discovery response.
        TSM tsm = new(Engines.PubSub, MessageToken.DiscoveryRequest, Engines.PubSub);
        TheProcessMessage tpm = new (tsm);
        comLineSubscriptionRegistry.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpm);
        comLineSubscriptionRegistry.Received().AnswerToSender(Arg.Is<TSM>(t => t.TXT == MessageToken.DiscoveryRequest), Arg.Is<TSM>(t => t.TXT == MessageToken.DiscoveryResponse));
        var registryIdent = tsmResult!.PLS;
        comLineSubscriptionRegistry.ClearReceivedCalls();

        // Send a subscribe-alive request and receive a subscribe trigger request.
        tsm = new(Engines.PubSub, MessageToken.SubscriberAlive, registryIdent);
        tpm = new(tsm);
        comLineSubscriptionRegistry.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpm);
        comLineSubscriptionRegistry.Received().AnswerToSender(Arg.Is<TSM>(t => t.TXT == MessageToken.SubscriberAlive),
            Arg.Is<TSM>(t => t.TXT == MessageToken.SubscribeTrigger && t.PLS.Equals(registryIdent)));
        comLineSubscriptionRegistry.ClearReceivedCalls();

        var tpmRegistry = CheckSubscribe(comLineSubscriptionRegistry);

        // Send a subscribe alive request with registryIdentity and receive no registry alive request.
        tsm = new(Engines.PubSub, MessageToken.SubscriberAlive, registryIdent);
        tpm = new(tsm);
        comLineSubscriptionRegistry.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpm);
        comLineSubscriptionRegistry.DidNotReceive<ComLine>().AnswerToSender(Arg.Any<TSM>(), Arg.Any<TSM>());

        // Send a subscribe alive request without registryIdentity and receive a registry alive request
        // (this case tests the backward compatibility).
        tsm = new(Engines.PubSub, MessageToken.SubscriberAlive, string.Empty);
        tpm = new(tsm);
        comLineSubscriptionRegistry.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpm);
        comLineSubscriptionRegistry.Received().AnswerToSender(Arg.Is<TSM>(t => t.TXT == MessageToken.SubscriberAlive),
            Arg.Is<TSM>(t => t.TXT == MessageToken.RegistryAlive && t.PLS.Equals(registryIdent)));
        comLineSubscriptionRegistry.ClearReceivedCalls();

        var tpmPublish = CheckPublish(comLineSubscriptionRegistry);            

        // Send an unsubscribe request and receive no response.
        tsm = new(Engines.PubSub, MessageToken.Unsubscribe, tpmRegistry.Message.PLS);
        tpm = new(tsm);
        comLineSubscriptionRegistry.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpm);
        comLineSubscriptionRegistry.DidNotReceive<ComLine>().AnswerToSender(Arg.Any<TSM>(), Arg.Any<TSM>());

        // Send a publish request and receive no response.
        comLineSubscriptionRegistry.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpmPublish);
        comLineSubscriptionRegistry.DidNotReceive<ComLine>().AnswerToSender(Arg.Any<TSM>(), Arg.Any<TSM>());

        // Prepare the subscriber shutdown and timer request
        CheckSubscribe(comLineSubscriptionRegistry);
        CheckPublish(comLineSubscriptionRegistry);

        // Send a subscriber shutdown request and receive no response.
        tsm = new(Engines.PubSub, MessageToken.SubscriberShutdown);
        tpm = new(tsm);
        comLineSubscriptionRegistry.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpm);
        comLineSubscriptionRegistry.DidNotReceive<ComLine>().AnswerToSender(Arg.Any<TSM>(), Arg.Any<TSM>());

        // Send a publish request and receive no response.
        comLineSubscriptionRegistry.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpmPublish);
        comLineSubscriptionRegistry.DidNotReceive<ComLine>().AnswerToSender(Arg.Any<TSM>(), Arg.Any<TSM>());
    }

    private static TheProcessMessage CheckSubscribe(ComLine comLineSubscriptionRegistry)
    {
        // Send a subscribe request and receive a subscribe response with the given GUID
        // and the latest version (use this subscription in the next step to publish a message).
        var guid = Guid.NewGuid();
        RegistrySubscriptionRequest rsr = new()
        {
            id = guid.ToString("N"),
            topics = ["topic"],
            isRegistry = true,
            version = PubSubVersion.V3
        };
        TSM tsm = new(Engines.PubSub, MessageToken.SubscribeRequest, TheCommonUtils.SerializeObjectToJSONString(rsr));
        TheProcessMessage tpm = new(tsm);
        comLineSubscriptionRegistry.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpm);
        comLineSubscriptionRegistry.Received()
            .AnswerToSender(Arg.Is<TSM>(t => t.TXT == MessageToken.SubscribeRequest),
        Arg.Is<TSM>(t => t.TXT.Equals(MessageToken.SubscribeResponse)
                         && TheCommonUtils.DeserializeJSONStringToObject<RegistrySubscriptionResponse>(t.PLS).id.Equals(guid.ToString("N"))
                         && TheCommonUtils.DeserializeJSONStringToObject<RegistrySubscriptionResponse>(t.PLS).version == PubSubVersion.Latest));
        comLineSubscriptionRegistry.ClearReceivedCalls();
        return tpm;
    }

    private static TheProcessMessage CheckPublish(ComLine comLineSubscriptionRegistry)
    {
        // Send a publish request and receive a publish request with the given
        // topic and payload.
        Message msg = new() { Topic = "topic", Payload = "This is a message" };
        TSM tsmPublish = new(Engines.RemotePubSub, $"{MessageToken.Publish}:topic|id|3.0.0", TheCommonUtils.SerializeObjectToJSONString(msg));
        TheProcessMessage tpmPublish = new(tsmPublish);
        comLineSubscriptionRegistry.MessageReceived += (MessageReceivedHandler)Raise.Event<MessageReceivedHandler>(null, tpmPublish);
        comLineSubscriptionRegistry.Received().AnswerToSender(Arg.Is<TSM>(t => t.TXT == MessageToken.SubscribeRequest),
            Arg.Is<TSM>(t => t.TXT.StartsWith(MessageToken.Publish)
                             && TheCommonUtils.DeserializeJSONStringToObject<Message>(t.PLS).Topic == msg.Topic));
                             //&& TheCommonUtils.DeserializeJSONStringToObject<Message>(t.PLS).Payload == msg.Payload));
        comLineSubscriptionRegistry.ClearReceivedCalls();
        return tpmPublish;
    }

}