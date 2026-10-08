// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Nats.WireFormat;
using TestUtilities;
using Xunit;
using INatsSubscriptionManager = SAF.Messaging.Nats.INatsSubscriptionManager;
using static TestWireFormat;

/// <summary>
/// <see cref="Messaging"/> leaves the wire format to the injected writer and reader.
/// </summary>
public class MessagingWireFormatTests
{
    private readonly IServiceMessageDispatcher _dispatcher = Substitute.For<IServiceMessageDispatcher>();
    private readonly INatsClient _client = Substitute.For<INatsClient>();
    private readonly INatsMessageWriter _writer = Substitute.For<INatsMessageWriter>();
    private readonly INatsMessageReader _reader = Substitute.For<INatsMessageReader>();
    private readonly MockLogger<Messaging> _logger = Substitute.For<MockLogger<Messaging>>();
    private readonly Messaging _messaging;

    public MessagingWireFormatTests()
    {
        _messaging = new Messaging(_logger, _client, Substitute.For<INatsSubscriptionManager>(),
            new NatsInputRouteTranslator(), new NatsOutputRouteTranslator(), _dispatcher, _writer, _reader);
    }

    [Fact]
    public void Publish_SendsWhatTheWriterProduces()
    {
        var message = new Message { Topic = "a/b" };
        var headers = new NatsHeaders { { "h", "v" } };
        WriterReturns(message, NatsWireMessage.Text("body", headers));

        _messaging.Publish(message);

        var arguments = _client.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "PublishAsync").GetArguments();
        Assert.Equal("a.b", arguments[0]);
        Assert.Equal("body", arguments[1]);
        Assert.Same(headers, arguments[2]);
    }

    [Fact]
    public void Publish_SendsABinaryBodyTheWriterProduces()
    {
        var message = new Message { Topic = "a/b" };
        byte[] binaryBody = [1, 2];
        WriterReturns(message, NatsWireMessage.Binary(binaryBody, null));

        _messaging.Publish(message);

        Assert.Same(binaryBody, _client.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "PublishAsync").GetArguments()[1]);
    }

    [Fact]
    public void Publish_DropsAMessageTheWriterCannotWriteAndLogsTheWritersReason()
    {
        _writer.TryWrite(Arg.Any<Message>(), out Arg.Any<NatsWireMessage>(), out Arg.Any<string?>())
            .Returns(ci =>
            {
                ci[2] = "the writer's reason";
                return false;
            });

        _messaging.Publish(new Message { Topic = "a/b", BinaryPayload = [1] });

        Assert.DoesNotContain(_client.ReceivedCalls(), c => c.GetMethodInfo().Name == "PublishAsync");
        _logger.AssertLogged(LogLevel.Error, m => m.Contains("a/b") && m.Contains("the writer's reason"));
    }

    [Fact]
    public async Task Subscribe_DispatchesOnlyWhatTheReaderReturns()
    {
        var read = new Message { Topic = "kept" };
        _reader.Read("a/dropped", Arg.Any<NatsBody>(), Arg.Any<NatsHeaders?>()).Returns((Message?)null);
        _reader.Read("a/kept", Arg.Any<NatsBody>(), Arg.Any<NatsHeaders?>()).Returns(read);
        var dispatched = new TaskCompletionSource<Message>();
        _dispatcher.When(d => d.DispatchMessage(Arg.Any<Action<Message>>(), Arg.Any<Message>()))
            .Do(ci => dispatched.TrySetResult(ci.ArgAt<Message>(1)));
        var channel = Subscribe();

        await channel.Writer.WriteAsync(Msg("a.dropped", "x"), TestContext.Current.CancellationToken);
        await channel.Writer.WriteAsync(Msg("a.kept", "y"), TestContext.Current.CancellationToken);

        Assert.Same(read, await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        _dispatcher.Received(1).DispatchMessage(Arg.Any<Action<Message>>(), Arg.Any<Message>());
    }

    [Fact]
    public async Task Subscribe_HandsTheBodyToTheReader()
    {
        byte[]? body = null;
        _reader.Read(Arg.Any<string>(), Arg.Any<NatsBody>(), Arg.Any<NatsHeaders?>())
            .Returns(ci =>
            {
                body = ci.ArgAt<NatsBody>(1).ToArray();
                return new Message { Topic = "t" };
            });
        var dispatched = new TaskCompletionSource();
        _dispatcher.When(d => d.DispatchMessage(Arg.Any<Action<Message>>(), Arg.Any<Message>())).Do(_ => dispatched.TrySetResult());
        var channel = Subscribe();

        await channel.Writer.WriteAsync(Msg("a.b", new byte[] { 0, 255 }), TestContext.Current.CancellationToken);

        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 0, 255 }, body);
    }

    private void WriterReturns(Message message, NatsWireMessage wireMessage)
        => _writer.TryWrite(message, out Arg.Any<NatsWireMessage>(), out Arg.Any<string?>())
            .Returns(ci =>
            {
                ci[1] = wireMessage;
                return true;
            });

    private Channel<NatsMsg<NatsMemoryOwner<byte>>> Subscribe()
    {
        var channel = Channel.CreateUnbounded<NatsMsg<NatsMemoryOwner<byte>>>();
        var subscription = Substitute.For<INatsSub<NatsMemoryOwner<byte>>>();
        subscription.Msgs.Returns(channel.Reader);
        _client.Connection
            .SubscribeCoreAsync<NatsMemoryOwner<byte>>(subject: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(subscription);
        _messaging.Subscribe(">", _ => { });
        return channel;
    }
}
