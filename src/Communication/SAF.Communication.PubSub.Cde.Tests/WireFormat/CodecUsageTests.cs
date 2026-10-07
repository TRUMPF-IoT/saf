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
    }

    [Fact]
    public async Task Broadcast_SendsWhatTheEncoderProducesForThePeerVersion()
    {
        var message = new Message { Topic = "t" };
        _encoder.Encode(message, PubSubVersion.V3).Returns("encoded");

        CreateRemoteSubscriber(PubSubVersion.V3).Broadcast(Broadcast(message));

        Assert.Equal("encoded", (await SentAsync()).PLS);
    }

    [Fact]
    public async Task Broadcast_SendsWhatTheBatchEncoderProducesForThePeerVersion()
    {
        var message = new Message { Topic = "t" };
        _encoder.EncodeBatch(Arg.Any<IEnumerable<Message>>(), PubSubVersion.V4).Returns("batch");

        CreateRemoteSubscriber(PubSubVersion.V4).Broadcast(Broadcast(message));

        Assert.Equal("batch", (await SentAsync()).PLS);
    }

    private RemoteSubscriber CreateRemoteSubscriber(string version)
        => new(_line, new TSM(Engines.PubSub, MessageToken.SubscribeRequest) { ORG = _line.Address }, ["*"],
            new RegistrySubscriptionRequest { version = version, isRegistry = false }, _encoder);

    private static BroadcastMessage Broadcast(Message message)
        => new(new Topic(message.Topic, "id", PubSubVersion.V4), message, "user", RoutingOptions.All);

    private Task<TSM> SentAsync()
        => _sent.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
}
