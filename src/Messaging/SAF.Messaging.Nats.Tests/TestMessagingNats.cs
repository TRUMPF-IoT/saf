﻿using NATS.Client.Core;
using NATS.Client.ObjectStore;
using NSubstitute;
using SAF.Common;
using Xunit;

namespace SAF.Messaging.Nats.Tests;

public class TestMessagingNats
{
    [Fact]
    public void RunMessaging()
    {
        var smd = Substitute.For<IServiceMessageDispatcher>();
        var subscriptionManager = Substitute.For<INatsSubscriptionManager>();
        var natsClient = Substitute.For<INatsClient>();

        var messaging = new Messaging(null, natsClient, subscriptionManager, smd, null);
        messaging.Unsubscribe(null!);
        subscriptionManager.DidNotReceive().TryRemove(Arg.Any<Guid>(), out _);

        messaging.Unsubscribe("");
        subscriptionManager.DidNotReceive().TryRemove(Arg.Any<Guid>(), out _);

        var subscriptionId = Guid.NewGuid();
        messaging.Unsubscribe(subscriptionId);
        subscriptionManager.Received().TryRemove(Arg.Is<Guid>(subscriptionId), out _);
        subscriptionManager.ClearReceivedCalls();

        var messageHandler = Substitute.For<IMessageHandler>();
        messageHandler.CanHandle(Arg.Any<Message>()).Returns(true);
        var id = (Guid)messaging.Subscribe<IMessageHandler>();
        natsClient.SubscribeAsync<string>(subject: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
        natsClient.Received().SubscribeAsync<string>(subject: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
        subscriptionManager.Received().TryAdd(Arg.Is<Guid>(id), Arg.Any<(string routeFilterPattern, CancellationTokenSource cancellationTokenSource, Task)>());
        subscriptionManager.ClearReceivedCalls();
        natsClient.ClearReceivedCalls();

        Message msg = new();
        msg.Topic = "Top";
        msg.Payload = "Payxx";
        messaging.Publish(msg);
        natsClient.Received().PublishAsync(subject: Arg.Is<string>(msg.Topic), data: Arg.Is<string>(msg.Payload));
        natsClient.ClearReceivedCalls();

        messaging.Unsubscribe(id);
        subscriptionManager.Received().TryRemove(Arg.Is<Guid>(id), out _);
    }

    [Fact]
    public void RunRedisMessagingConfiguration()
    {
        int defaultTimeout = 30;
        int defaultMaxReconnectRetry = -1;
        NatsMessagingConfiguration nmc = new();

        Assert.Null(nmc.Url);
        Assert.False(nmc.Verbose);
        Assert.StrictEqual(nmc.ConnectionTimeout, defaultTimeout);
        Assert.StrictEqual(nmc.RequestTimeout, defaultTimeout);
        Assert.StrictEqual(nmc.CommandTimeout, defaultTimeout);
        Assert.StrictEqual(nmc.MaxReconnectRetry, defaultMaxReconnectRetry);
        Assert.Null(nmc.Username);
        Assert.Null(nmc.Password);
        Assert.Null(nmc.Token);
        Assert.Null(nmc.Jwt);
        Assert.Null(nmc.NKey);
        Assert.Null(nmc.Seed);
        Assert.Null(nmc.CredsFile);
        Assert.Null(nmc.NKeyFile);


        MessagingConfiguration mc = new();
        mc.Config = new Dictionary<string, string>();
        mc.Config.Add("url", "nats://localhost:2222");
        mc.Config.Add("verbose", "true");
        mc.Config.Add("connectionTimeoutInSeconds", "60");
        mc.Config.Add("requestTimeoutInSeconds", "50");
        mc.Config.Add("commandTimeoutInSeconds", "40");
        mc.Config.Add("maxReconnectRetry", "15");
        mc.Config.Add("authOpt:username", "natsUser");
        mc.Config.Add("authOpt:password", "HighSecretPassword");
        mc.Config.Add("authOpt:token", "Token123456789");
        mc.Config.Add("authOpt:jwt", "alongjwttoken");
        mc.Config.Add("authOpt:nkey", "AABBCCDDEEFF");
        mc.Config.Add("authOpt:seed", "FFEEDDCCBBAA");
        mc.Config.Add("authOpt:credsFile", "C:\\myCredFile.key");
        mc.Config.Add("authOpt:nkeyFile", "C:\\myNKeyFile.nkey");


        nmc = new(mc);
        Assert.Equal("nats://localhost:2222", nmc.Url);
        Assert.True(nmc.Verbose);
        Assert.StrictEqual(60, nmc.ConnectionTimeout);
        Assert.StrictEqual(50, nmc.RequestTimeout);
        Assert.StrictEqual(40, nmc.CommandTimeout);
        Assert.StrictEqual(15, nmc.MaxReconnectRetry);
        Assert.Equal("natsUser", nmc.Username);
        Assert.Equal("HighSecretPassword", nmc.Password);
        Assert.Equal("Token123456789", nmc.Token);
        Assert.Equal("alongjwttoken", nmc.Jwt);
        Assert.Equal("AABBCCDDEEFF", nmc.NKey);
        Assert.Equal("FFEEDDCCBBAA", nmc.Seed);
        Assert.Equal("C:\\myCredFile.key", nmc.CredsFile);
        Assert.Equal("C:\\myNKeyFile.nkey", nmc.NKeyFile);
    }

    [Fact]
    public void RunStorage()
    {
        var natsObjContext = Substitute.For<INatsObjContext>();
        var natsObjStore = Substitute.For<INatsObjStore>();
        var globalStorageArea = "global";

        natsObjContext.CreateObjectStoreAsync(Arg.Any<string>()).Returns(natsObjStore);

        byte[] value = { 118, 97, 108, 117, 101 };
        Storage storage = new(natsObjContext);

        storage.Set("area", "key", value);
        natsObjContext.Received().CreateObjectStoreAsync(Arg.Is<string>("area"));
        natsObjStore.Received().PutAsync(Arg.Is<string>("key"), Arg.Is<byte[]>(value));

        natsObjContext.ClearReceivedCalls();
        natsObjStore.ClearReceivedCalls();

        storage.Set("key", value);
        natsObjContext.Received().CreateObjectStoreAsync(Arg.Is<string>(globalStorageArea));
        natsObjStore.Received().PutAsync(Arg.Is<string>("key"), Arg.Is<byte[]>(value));

        natsObjContext.ClearReceivedCalls();
        natsObjStore.ClearReceivedCalls();

        storage.GetBytes("area", "key");
        natsObjContext.Received().CreateObjectStoreAsync(Arg.Is<string>("area"));
        natsObjStore.GetBytesAsync(Arg.Is<string>("key"));

        natsObjContext.ClearReceivedCalls();
        natsObjStore.ClearReceivedCalls();

        storage.GetBytes("key");
        natsObjContext.Received().CreateObjectStoreAsync(Arg.Is<string>(globalStorageArea));
        natsObjStore.GetBytesAsync(Arg.Is<string>("key"));

        natsObjContext.ClearReceivedCalls();
        natsObjStore.ClearReceivedCalls();

        storage.RemoveKey("area", "key");
        natsObjContext.Received().CreateObjectStoreAsync(Arg.Is<string>("area"));
        natsObjStore.DeleteAsync("key");

        natsObjContext.ClearReceivedCalls();
        natsObjStore.ClearReceivedCalls();

        storage.RemoveKey("key");
        natsObjContext.Received().CreateObjectStoreAsync(Arg.Is<string>(globalStorageArea));
        natsObjStore.DeleteAsync("key");

        natsObjContext.ClearReceivedCalls();
        natsObjStore.ClearReceivedCalls();

        storage.RemoveArea("area");
        natsObjContext.Received().DeleteObjectStore(Arg.Is<string>("area"), CancellationToken.None);
    }
}
