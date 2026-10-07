// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.Tests.WireFormat;

using System.Threading.Channels;
using NATS.Client.Core;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.Nats.WireFormat;
using Xunit;
using INatsSubscriptionManager = SAF.Messaging.Nats.INatsSubscriptionManager;

/// <summary>
/// <see cref="Messaging"/> leaves the wire format to the injected writer and reader.
/// </summary>
public class MessagingWireFormatTests
{
    private readonly IServiceMessageDispatcher _dispatcher = Substitute.For<IServiceMessageDispatcher>();
    private readonly INatsClient _client = Substitute.For<INatsClient>();
    private readonly INatsMessageWriter _writer = Substitute.For<INatsMessageWriter>();
    private readonly INatsMessageReader _reader = Substitute.For<INatsMessageReader>();
    private readonly Messaging _messaging;

    public MessagingWireFormatTests()
    {
        _messaging = new Messaging(null, _client, Substitute.For<INatsSubscriptionManager>(),
            new NatsInputRouteTranslator(), new NatsOutputRouteTranslator(), _dispatcher, _writer, _reader);
    }

    [Fact]
    public void Publish_SendsWhatTheWriterProduces()
    {
        var message = new Message { Topic = "a/b" };
        var headers = new NatsHeaders { { "h", "v" } };
        _writer.Write(message).Returns(new NatsWireMessage("body", headers));

        _messaging.Publish(message);

        var arguments = _client.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "PublishAsync").GetArguments();
        Assert.Equal("a.b", arguments[0]);
        Assert.Equal("body", arguments[1]);
        Assert.Same(headers, arguments[2]);
    }

    [Fact]
    public async Task Subscribe_DispatchesOnlyWhatTheReaderReturns()
    {
        var read = new Message { Topic = "kept" };
        _reader.Read("a/dropped", Arg.Any<string?>(), Arg.Any<NatsHeaders?>()).Returns((Message?)null);
        _reader.Read("a/kept", Arg.Any<string?>(), Arg.Any<NatsHeaders?>()).Returns(read);
        var dispatched = new TaskCompletionSource<Message>();
        _dispatcher.When(d => d.DispatchMessage(Arg.Any<Action<Message>>(), Arg.Any<Message>()))
            .Do(ci => dispatched.TrySetResult(ci.ArgAt<Message>(1)));

        var channel = Channel.CreateUnbounded<NatsMsg<string>>();
        var subscription = Substitute.For<INatsSub<string>>();
        subscription.Msgs.Returns(channel.Reader);
        _client.Connection
            .SubscribeCoreAsync<string>(subject: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(subscription);
        _messaging.Subscribe(">", _ => { });

        await channel.Writer.WriteAsync(new NatsMsg<string>("a.dropped", null, 0, null, null, null), TestContext.Current.CancellationToken);
        await channel.Writer.WriteAsync(new NatsMsg<string>("a.kept", null, 0, null, null, null), TestContext.Current.CancellationToken);

        Assert.Same(read, await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        _dispatcher.Received(1).DispatchMessage(Arg.Any<Action<Message>>(), Arg.Any<Message>());
    }
}
