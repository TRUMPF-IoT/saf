// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests;
using NSubstitute;
using Common;
using StackExchange.Redis;
using System.Net;
using Xunit;

public class TestMessagingRedis
{
    [Fact]
    public void RunMessaging()
    {
        var smd = Substitute.For<IServiceMessageDispatcher>();
        var connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        connectionMultiplexer.GetSubscriber().Returns(subscriber);

        Messaging messaging = new(null, connectionMultiplexer, smd, null);
        messaging.Unsubscribe(null!);
        subscriber.DidNotReceive().Unsubscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>());
        messaging.Unsubscribe("");
        subscriber.DidNotReceive().Unsubscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>());
        messaging.Unsubscribe(Guid.NewGuid());
        subscriber.DidNotReceive().Unsubscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>());

        var messageHandler = Substitute.For<IMessageHandler>();
        messageHandler.CanHandle(Arg.Any<Message>()).Returns(true);
        var id = (Guid)messaging.Subscribe<IMessageHandler>();
        subscriber.Received().Subscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>());
        subscriber.ClearReceivedCalls();

        Message msg = new();
        msg.Topic = "Top";
        msg.Payload = "Payxx";
        messaging.Publish(msg);
        subscriber.Received().Publish(Arg.Is(RedisChannel.Literal("Top")), Arg.Is<RedisValue>(v => v.ToString().Contains("Payxx")), Arg.Is(CommandFlags.FireAndForget));
        subscriber.ClearReceivedCalls();

        messaging.Unsubscribe(id);
        subscriber.Received().Unsubscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>());
        subscriber.ClearReceivedCalls();
    }

    [Fact]
    public void RunRedisMessagingConfiguration()
    {
        RedisMessagingConfiguration rmc = new();
        Assert.Null(rmc.ConnectionString);

        MessagingConfiguration mc = new();
        mc.Config = new Dictionary<string, string>();
        mc.Config.Add("connectionString", "aConnectionString");
        rmc = new(mc);
        Assert.Equal("aConnectionString", rmc.ConnectionString);
    }

    [Fact]
    public void RunStorage()
    {
        var database = Substitute.For<IDatabase>();
        var server = Substitute.For<IServer>();
        var connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
        connectionMultiplexer.GetDatabase().Returns(database);
        connectionMultiplexer.GetServer(Arg.Any<EndPoint>(), Arg.Any<object>()).Returns(server);

        byte[] byteArray = { 37, 241 };
        Storage storage = new(connectionMultiplexer, ConfigurationOptions.Parse("localhost"));
        Assert.Throws<Exception>(() => storage.Set("area", "key", "value"));
        Assert.Throws<Exception>(() => storage.Set("area", "keyByte", byteArray));
        database.StringSet(Arg.Any<RedisKey>(), Arg.Any<RedisValue>()).Returns(true);
        storage.Set("area", "key", "value");
        database.Received().StringSet(Arg.Is<RedisKey>("area:key"), Arg.Is<RedisValue>("value"));
        database.ClearReceivedCalls();
        storage.GetString("area", "key");
        database.Received().StringGet(Arg.Is<RedisKey>("area:key"));
        database.ClearReceivedCalls();

        storage.Set("area", "keyByte", byteArray);
        database.Received().StringSet(Arg.Is<RedisKey>("area:keybyte"), Arg.Is<RedisValue>(byteArray));
        database.ClearReceivedCalls();
        storage.GetBytes("area", "keyByte");
        database.Received().StringGet(Arg.Is<RedisKey>("area:keybyte"));
        database.ClearReceivedCalls();

        storage.RemoveKey("area", "key");
        database.Received().KeyDelete(Arg.Is<RedisKey>("area:key"));
        database.ClearReceivedCalls();
        storage.RemoveKey("key");
        database.Received().KeyDelete(Arg.Is<RedisKey>("global:key"));
        database.ClearReceivedCalls();

        storage.RemoveArea("area");
        server.Received().Keys(Arg.Is(-1), Arg.Is<RedisValue>("area:*"));
        database.Received().KeyDelete(Arg.Any<RedisKey[]>());
        server.ClearReceivedCalls();
        database.ClearReceivedCalls();

        Assert.Throws<NotSupportedException>(() => storage.RemoveArea("global"));
    }
}