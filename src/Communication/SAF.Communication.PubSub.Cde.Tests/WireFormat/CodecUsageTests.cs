// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests.WireFormat;

using Communication.Cde;
using Interfaces;
using nsCDEngine.BaseClasses;
using NSubstitute;
using SAF.Communication.PubSub.Cde.MessageProcessing;
using SAF.Communication.PubSub.Cde.WireFormat;
using SAF.Messaging.Contracts;
using Xunit;

/// <summary>
/// <see cref="RemoteSubscriber"/> leaves the wire format to the injected encoder.
/// </summary>
public class CodecUsageTests
{
    private readonly ComLine _line = Substitute.For<ComLine>();
    private readonly ITsmMessageEncoder _encoder = Substitute.For<ITsmMessageEncoder>();
    private readonly TaskCompletionSource<TSM> _sent = new();

    public CodecUsageTests()
    {
        _line.Address.Returns("origin");
        _line.When(l => l.AnswerToSender(Arg.Any<TSM>(), Arg.Any<TSM>()))
            .Do(ci => _sent.TrySetResult(ci.ArgAt<TSM>(1)));
        _encoder.CanEncode(Arg.Any<Message>(), Arg.Any<string>()).Returns(true);
    }

    [Fact]
    public async Task Broadcast_SendsWhatTheEncoderProducesForThePeerVersion()
    {
        var message = new Message { Topic = "t" };
        _encoder.Encode(message, PubSubVersion.V3).Returns(new TsmPayload("encoded"));

        CreateRemoteSubscriber(PubSubVersion.V3).Broadcast(Broadcast(message));

        Assert.Equal("encoded", (await SentAsync()).PLS);
    }

    [Fact]
    public async Task Broadcast_SendsWhatTheBatchEncoderProducesForThePeerVersion()
    {
        var message = new Message { Topic = "t" };
        _encoder.EncodeBatch(Arg.Any<IEnumerable<Message>>(), PubSubVersion.V4).Returns(new TsmPayload("batch"));

        CreateRemoteSubscriber(PubSubVersion.V4).Broadcast(Broadcast(message));

        Assert.Equal("batch", (await SentAsync()).PLS);
    }

    [Fact]
    public async Task Broadcast_SendsThePlbTheEncoderProduces()
    {
        byte[] plb = [1, 2, 3];
        _encoder.EncodeBatch(Arg.Any<IEnumerable<Message>>(), PubSubVersion.V5).Returns(new TsmPayload("batch", plb));

        CreateRemoteSubscriber(PubSubVersion.V5).Broadcast(Broadcast(new Message { Topic = "t" }));

        var sent = await SentAsync();
        Assert.EndsWith($"|{PubSubVersion.V5}", sent.TXT);
        Assert.Same(plb, sent.PLB);
    }

    [Theory]
    [InlineData(PubSubVersion.V3)]
    [InlineData("7.1.0")]
    public async Task Broadcast_EncodesForTheNewestVersionBothSidesKnow(string peerVersion)
    {
        var expected = peerVersion == PubSubVersion.V3 ? PubSubVersion.V3 : PubSubVersion.Latest;
        _encoder.Encode(Arg.Any<Message>(), expected).Returns(new TsmPayload("single"));
        _encoder.EncodeBatch(Arg.Any<IEnumerable<Message>>(), expected).Returns(new TsmPayload("batch"));

        CreateRemoteSubscriber(peerVersion).Broadcast(Broadcast(new Message { Topic = "t" }));

        var sent = await SentAsync();
        Assert.EndsWith($"|{expected}", sent.TXT);
        _encoder.Received().CanEncode(Arg.Any<Message>(), expected);
    }

    [Theory]
    [InlineData(PubSubVersion.V3)]
    [InlineData(PubSubVersion.V4)]
    public async Task Broadcast_DropsAMessageThePeerCannotReceive(string version)
    {
        var dropped = new Message { Topic = "dropped" };
        var sent = new Message { Topic = "sent" };
        _encoder.CanEncode(dropped, version).Returns(false);
        _encoder.Encode(sent, version).Returns(new TsmPayload("encoded"));
        _encoder.EncodeBatch(Arg.Any<IEnumerable<Message>>(), version)
            .Returns(ci => new TsmPayload(string.Join(",", ci.Arg<IEnumerable<Message>>().Select(m => m.Topic))));
        var remote = CreateRemoteSubscriber(version);

        remote.Broadcast(Broadcast(dropped));
        remote.Broadcast(Broadcast(sent));

        Assert.Equal(version == PubSubVersion.V4 ? "sent" : "encoded", (await SentAsync()).PLS);
        _encoder.DidNotReceive().Encode(dropped, Arg.Any<string>());
    }

    private RemoteSubscriber CreateRemoteSubscriber(string version)
        => new(_line, new TSM(Engines.PubSub, MessageToken.SubscribeRequest) { ORG = _line.Address }, ["*"],
            new RegistrySubscriptionRequest { version = version, isRegistry = false }, _encoder);

    private static BroadcastMessage Broadcast(Message message)
        => new(new Topic(message.Topic, "id", PubSubVersion.V4), message, "user", RoutingOptions.All);

    private Task<TSM> SentAsync()
        => _sent.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
}
