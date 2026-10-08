// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests;

using Communication.Cde;
using Interfaces;
using nsCDEngine.BaseClasses;
using nsCDEngine.Engines.ThingService;
using nsCDEngine.ViewModels;
using NSubstitute;
using SAF.Communication.PubSub.Cde.WireFormat;
using Xunit;

/// <summary>
/// A remote publisher, such as a browser node, sends its messages to the registry, which passes them on.
/// </summary>
public class SubscriptionRegistryTests
{
    private readonly ComLine _line = Substitute.For<ComLine>();
    private readonly TaskCompletionSource<TSM> _published = new();

    public SubscriptionRegistryTests()
    {
        _line.Address.Returns("registry");
        _line.When(l => l.AnswerToSender(Arg.Any<TSM>(), Arg.Is<TSM>(t => t.TXT.StartsWith(MessageToken.Publish))))
            .Do(ci => _published.TrySetResult(ci.ArgAt<TSM>(1)));

        var codec = TsmWireFormats.CreateCodec();
        new SubscriptionRegistry(_line, codec, codec).ConnectAsync(CancellationToken.None).Wait();
    }

    [Fact]
    public async Task Publication_PassesBinaryPayloadsOn()
    {
        Subscribe(PubSubVersion.V5);

        Receive(new TSM(Engines.RemotePubSub, $"{MessageToken.Publish}:$$batch:size=1$$|id|{PubSubVersion.V5}",
            """[{"Topic":"t","BinaryPayloadLength":3}]""") { PLB = [7, 8, 9] });

        var sent = await _published.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal("""[{"Topic":"t","BinaryPayloadLength":3}]""", sent.PLS);
        Assert.Equal(new byte[] { 7, 8, 9 }, sent.PLB);
    }

    [Fact]
    public void Publication_IsDroppedWhenItCannotBeRead()
    {
        Subscribe(PubSubVersion.V3);

        Receive(new TSM(Engines.RemotePubSub, $"{MessageToken.Publish}:t|id|{PubSubVersion.V5}", """{"Topic":"t","BinaryPayloadLength":2}"""));

        _line.DidNotReceive().AnswerToSender(Arg.Any<TSM>(), Arg.Is<TSM>(t => t.TXT.StartsWith(MessageToken.Publish)));
    }

    private void Subscribe(string version)
    {
        var request = new RegistrySubscriptionRequest { id = "id", topics = ["t"], isRegistry = true, version = version };
        Receive(new TSM(Engines.PubSub, MessageToken.SubscribeRequest, TheCommonUtils.SerializeObjectToJSONString(request)) { ORG = "subscriber" });
    }

    private void Receive(TSM tsm)
        => _line.MessageReceived += Raise.Event<MessageReceivedHandler>(Substitute.For<ICDEThing>(), new TheProcessMessage(tsm));
}
