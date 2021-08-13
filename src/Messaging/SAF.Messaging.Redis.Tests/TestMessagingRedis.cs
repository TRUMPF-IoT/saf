// SPDX-FileCopyrightText: 2017-2021 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using NSubstitute;
using SAF.Common;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using Xunit;

namespace SAF.Messaging.Redis.Tests
{
    public class TestMessagingRedis
    {
        [Fact]
        public void RunMessaging()
        {
            IServiceMessageDispatcher smd = Substitute.For<IServiceMessageDispatcher>();
            IConnectionMultiplexer connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
            ISubscriber subscriber = Substitute.For<ISubscriber>();
            connectionMultiplexer.GetSubscriber().Returns(subscriber);

            Messaging messaging = new(null, connectionMultiplexer, smd, null);
            messaging.Unsubscribe(null);
            subscriber.DidNotReceive().Unsubscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>());
            messaging.Unsubscribe("");
            subscriber.DidNotReceive().Unsubscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>());
            messaging.Unsubscribe(Guid.NewGuid());
            subscriber.DidNotReceive().Unsubscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>());

            IMessageHandler messageHandler = Substitute.For<IMessageHandler>();
            messageHandler.CanHandle(Arg.Any<Message>()).Returns(true);
            Guid id = (Guid)messaging.Subscribe<IMessageHandler>();
            subscriber.Received().Subscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>());
            subscriber.ClearReceivedCalls();

            Message msg = new();
            msg.Topic = "Top";
            msg.Payload = "Payxx";
            messaging.Publish(msg);
            subscriber.Received().Publish(Arg.Is<RedisChannel>("Top"), Arg.Is<RedisValue>(v => v.ToString().Contains("Payxx")), Arg.Is(CommandFlags.FireAndForget));
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
            IDatabase database = Substitute.For<IDatabase>();
            IConnectionMultiplexer connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
            connectionMultiplexer.GetDatabase().Returns(database);

            byte[] byteArray = new byte[2] { 37, 241 };
            Storage storage = new(connectionMultiplexer);
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

        }
    }
}
