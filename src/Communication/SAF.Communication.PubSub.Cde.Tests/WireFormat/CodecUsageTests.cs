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
    private readonly ITsmPeerEncoder _peerEncoder = Substitute.For<ITsmPeerEncoder>();
    private readonly IDroppedMessageWarning _droppedMessageWarning = Substitute.For<IDroppedMessageWarning>();
    private readonly TaskCompletionSource<TSM> _sent = new();

    public CodecUsageTests()
    {
        _line.Address.Returns("origin");
        _line.When(l => l.AnswerToSender(Arg.Any<TSM>(), Arg.Any<TSM>()))
            .Do(ci => _sent.TrySetResult(ci.ArgAt<TSM>(1)));
        _encoder.ForPeer(Arg.Any<string>()).Returns(_peerEncoder);
        _peerEncoder.CanEncode(Arg.Any<Message>()).Returns(true);
    }

    [Fact]
    public async Task Broadcast_SendsWhatTheEncoderProducesForThePeerVersion()
    {
        var message = new Message { Topic = "t" };
        _peerEncoder.Encode(message).Returns(new TsmPayload("encoded"));

        CreateRemoteSubscriber(PubSubVersion.V3).Broadcast(Broadcast(message));

        Assert.Equal("encoded", (await SentAsync()).PLS);
        _encoder.Received(1).ForPeer(PubSubVersion.V3);
    }

    [Fact]
    public async Task Broadcast_SendsWhatTheBatchEncoderProducesForThePeerVersion()
    {
        _peerEncoder.EncodeBatch(Arg.Any<IEnumerable<Message>>()).Returns(new TsmPayload("batch"));

        CreateRemoteSubscriber(PubSubVersion.V4).Broadcast(Broadcast(new Message { Topic = "t" }));

        Assert.Equal("batch", (await SentAsync()).PLS);
        _encoder.Received(1).ForPeer(PubSubVersion.V4);
    }

    [Fact]
    public async Task Broadcast_SendsThePlbTheEncoderProduces()
    {
        byte[] plb = [1, 2, 3];
        _peerEncoder.EncodeBatch(Arg.Any<IEnumerable<Message>>()).Returns(new TsmPayload("batch", plb));

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
        _peerEncoder.Encode(Arg.Any<Message>()).Returns(new TsmPayload("single"));
        _peerEncoder.EncodeBatch(Arg.Any<IEnumerable<Message>>()).Returns(new TsmPayload("batch"));

        CreateRemoteSubscriber(peerVersion).Broadcast(Broadcast(new Message { Topic = "t" }));

        var sent = await SentAsync();
        Assert.EndsWith($"|{expected}", sent.TXT);
        _encoder.Received(1).ForPeer(expected);
    }

    [Fact]
    public void Constructor_PicksTheFormatOnce()
    {
        var remote = CreateRemoteSubscriber(PubSubVersion.V3);
        _peerEncoder.Encode(Arg.Any<Message>()).Returns(new TsmPayload("single"));

        remote.Broadcast(Broadcast(new Message { Topic = "a" }));
        remote.Broadcast(Broadcast(new Message { Topic = "b" }));

        _encoder.Received(1).ForPeer(Arg.Any<string>());
    }

    [Theory]
    [InlineData(PubSubVersion.V3)]
    [InlineData(PubSubVersion.V4)]
    public async Task Broadcast_DropsAMessageThePeerCannotReceiveAndReportsIt(string version)
    {
        var dropped = new Message { Topic = "dropped" };
        var sent = new Message { Topic = "sent" };
        _peerEncoder.CanEncode(dropped).Returns(false);
        _peerEncoder.Encode(sent).Returns(new TsmPayload("encoded"));
        _peerEncoder.EncodeBatch(Arg.Any<IEnumerable<Message>>())
            .Returns(ci => new TsmPayload(string.Join(",", ci.Arg<IEnumerable<Message>>().Select(m => m.Topic))));
        var remote = CreateRemoteSubscriber(version);

        remote.Broadcast(Broadcast(dropped));
        remote.Broadcast(Broadcast(sent));

        Assert.Equal(version == PubSubVersion.V4 ? "sent" : "encoded", (await SentAsync()).PLS);
        _peerEncoder.DidNotReceive().Encode(dropped);
        _droppedMessageWarning.Received(1).Report("origin", version, dropped);
        _droppedMessageWarning.DidNotReceive().Report(Arg.Any<string>(), Arg.Any<string>(), sent);
    }

    private RemoteSubscriber CreateRemoteSubscriber(string version)
        => new(_line, new TSM(Engines.PubSub, MessageToken.SubscribeRequest) { ORG = _line.Address }, ["*"],
            new RegistrySubscriptionRequest { version = version, isRegistry = false }, _encoder, _droppedMessageWarning);

    private static BroadcastMessage Broadcast(Message message)
        => new(new Topic(message.Topic, "id", PubSubVersion.V4), message, "user", RoutingOptions.All);

    private Task<TSM> SentAsync()
        => _sent.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
}
