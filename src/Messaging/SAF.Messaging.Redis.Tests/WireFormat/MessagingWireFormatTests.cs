// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Redis.WireFormat;
using StackExchange.Redis;
using Xunit;

/// <summary>
/// <see cref="Messaging"/> leaves the wire format to the injected writer and reader.
/// </summary>
public class MessagingWireFormatTests
{
    private readonly IServiceMessageDispatcher _dispatcher = Substitute.For<IServiceMessageDispatcher>();
    private readonly ISubscriber _subscriber = Substitute.For<ISubscriber>();
    private readonly IRedisMessageWriter _writer = Substitute.For<IRedisMessageWriter>();
    private readonly IRedisMessageReader _reader = Substitute.For<IRedisMessageReader>();
    private readonly Messaging _messaging;

    public MessagingWireFormatTests()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetSubscriber().Returns(_subscriber);
        _messaging = new Messaging(null, multiplexer, _dispatcher, _writer, _reader);
    }

    [Fact]
    public void Publish_SendsWhatTheWriterProduces()
    {
        var message = new Message { Topic = "t" };
        _writer.Write(message).Returns("written");

        _messaging.Publish(message);

        _subscriber.Received(1).Publish(RedisChannel.Literal("t"), "written", CommandFlags.FireAndForget);
    }

    [Fact]
    public void Subscribe_DispatchesWhatTheReaderReturns()
    {
        var read = new Message { Topic = "t" };
        _reader.Read("channel", "value").Returns(read);

        CaptureInternalHandler()(RedisChannel.Literal("channel"), "value");

        _dispatcher.Received(1).DispatchMessage(Arg.Any<Action<Message>>(), read);
    }

    [Fact]
    public void Subscribe_DispatchesNothing_WhenTheReaderDropsTheValue()
    {
        _reader.Read(Arg.Any<string>(), Arg.Any<string>()).Returns((Message?)null);

        CaptureInternalHandler()(RedisChannel.Literal("channel"), "value");

        _dispatcher.DidNotReceive().DispatchMessage(Arg.Any<Action<Message>>(), Arg.Any<Message>());
    }

    private Action<RedisChannel, RedisValue> CaptureInternalHandler()
    {
        Action<RedisChannel, RedisValue>? internalHandler = null;
        _subscriber.When(s => s.Subscribe(Arg.Any<RedisChannel>(), Arg.Any<Action<RedisChannel, RedisValue>>(), Arg.Any<CommandFlags>()))
            .Do(ci => internalHandler = ci.ArgAt<Action<RedisChannel, RedisValue>>(1));

        _messaging.Subscribe("*", _ => { });

        Assert.NotNull(internalHandler);
        return internalHandler!;
    }
}
