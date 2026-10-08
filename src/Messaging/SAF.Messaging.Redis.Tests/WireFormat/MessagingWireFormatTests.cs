// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Redis.WireFormat;
using StackExchange.Redis;
using TestUtilities;
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
    private readonly MockLogger<Messaging> _logger = Substitute.For<MockLogger<Messaging>>();
    private readonly Messaging _messaging;

    public MessagingWireFormatTests()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetSubscriber().Returns(_subscriber);
        _messaging = new Messaging(_logger, multiplexer, _dispatcher, _writer, _reader);
    }

    [Fact]
    public void Publish_SendsWhatTheWriterProduces()
    {
        var message = new Message { Topic = "t" };
        _writer.TryWrite(message, out Arg.Any<RedisValue>(), out Arg.Any<string?>())
            .Returns(ci =>
            {
                ci[1] = (RedisValue)"written";
                return true;
            });

        _messaging.Publish(message);

        _subscriber.Received(1).Publish(RedisChannel.Literal("t"), "written", CommandFlags.FireAndForget);
    }

    [Fact]
    public void Publish_DropsAndLogsAMessageTheWriterCannotWrite()
    {
        _writer.TryWrite(Arg.Any<Message>(), out Arg.Any<RedisValue>(), out Arg.Any<string?>())
            .Returns(ci =>
            {
                ci[2] = "the writer's reason";
                return false;
            });

        _messaging.Publish(new Message { Topic = "a/b", BinaryPayload = [1] });

        _subscriber.DidNotReceive().Publish(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>());
        _logger.AssertLogged(LogLevel.Error, m => m.Contains("a/b") && m.Contains("the writer's reason"));
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
        _reader.Read(Arg.Any<string>(), Arg.Any<RedisValue>()).Returns((Message?)null);

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
