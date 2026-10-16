// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests;

using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Messaging.Contracts;
using Heartbeat;
using SAF.Toolbox.Serialization;
using TestUtilities;
using Xunit;
using IReplyTopics = SAF.Toolbox.RequestClient.IReplyTopics;
using RequestOptions = SAF.Toolbox.RequestClient.RequestOptions;

public class RequestClientTests
{
    private readonly IMessagingInfrastructure _messaging = Substitute.For<IMessagingInfrastructure>();
    private readonly IHeartbeat _heartbeat = Substitute.For<IHeartbeat>();
    private readonly MockLogger<RequestClient.RequestClient> _logger = Substitute.For<MockLogger<RequestClient.RequestClient>>();
    private Message? _published;

    public RequestClientTests()
    {
        _heartbeat.BeatCycleTimeMillis.Returns(1000);
    }

    [Fact]
    public async Task ReturnsValueOfResponseTimeout()
    {
        // Arrange
        string? replyToTopic = null;
        var messagingMock = Substitute.For<IMessagingInfrastructure>();
        messagingMock.When(m => m.Publish(Arg.Any<Message>()))
            .Do(ci =>
            {   // "catch" reply to of sent message
                var msg = JsonSerializer.Deserialize<DummyRequest>(ci.Arg<Message>().Payload!)!;
                replyToTopic = msg.ReplyTo;
            });

        var heartbeatMock = Substitute.For<IHeartbeat>();
        heartbeatMock.BeatCycleTimeMillis.Returns(1000);

        // Act
        var sut = new RequestClient.RequestClient(messagingMock, heartbeatMock, null);
        var task = sut.SendRequestAwaitFirstAnswer<DummyRequest, DummyResponse>("test", new DummyRequest());
        sut.Handle(new Message { Topic = replyToTopic!, Payload = "{ \"myDummyValue\": 123 }" });

        // Assert
        var response = await task;
        Assert.True(task.IsCompleted);
        Assert.NotNull(response);
        Assert.Equal(123, response.MyDummyValue);
    }

    [Fact]
    public async Task ReturnsNullOnTimeout()
    {
        // Arrange
        var messagingMock = Substitute.For<IMessagingInfrastructure>();
        var heartbeatMock = Substitute.For<IHeartbeat>();
        heartbeatMock.BeatCycleTimeMillis.Returns(1000);

        // Act
        var sut = new RequestClient.RequestClient(messagingMock, heartbeatMock, null);
        var task = sut.SendRequestAwaitFirstAnswer<DummyRequest, DummyResponse>("test", new DummyRequest(), millisecondsTimeoutTarget: 5000);

        heartbeatMock.Beat += Raise.EventWith(this, new HeartbeatEventArgs(1000, 5));
        var response = await task;

        // Assert
        Assert.True(task.IsCompleted);
        Assert.Null(response); // timed out!
    }

    [Fact]
    public async Task ReturnsNullOnDisposal()
    {
        // Arrange
        var messagingMock = Substitute.For<IMessagingInfrastructure>();
        var heartbeatMock = Substitute.For<IHeartbeat>();
        heartbeatMock.BeatCycleTimeMillis.Returns(1000);

        // Act
        var sut = new RequestClient.RequestClient(messagingMock, heartbeatMock, null);
        var task = sut.SendRequestAwaitFirstAnswer<DummyRequest, DummyResponse>("test", new DummyRequest(), millisecondsTimeoutTarget: 30000);

        await Task.Delay(10, TestContext.Current.CancellationToken);

        sut.Dispose();
        var response = await task;

        // Assert
        Assert.True(task.IsCompleted);
        Assert.Null(response); // canceled!
    }

    [Fact]
    public async Task SendRequestAwaitFirstAnswer_GetsATextReply_BecauseItAcceptsTextOnly()
    {
        var sut = CreateSut(ReplyWithBinaryIfAccepted);

        var response = await sut.SendRequestAwaitFirstAnswer<DummyRequest, DummyResponse>("test", new DummyRequest());

        Assert.Null(_published!.AcceptedReplyFormats);
        Assert.Null(_published.BinaryPayload);
        Assert.Equal(123, response!.MyDummyValue);
        _logger.AssertNotLogged(LogLevel.Warning);
    }

    /// <summary>
    /// SAF 10.x responders read the reply topic from the payload, SAF 11.x responders from the message.
    /// </summary>
    [Fact]
    public async Task SendRequestAwaitFirstAnswer_SendsTheReplyTopicInPayloadAndMessage()
    {
        var sut = CreateSut(message => new Message
        {
            Topic = JsonSerializer.Deserialize<DummyRequest>(message.Payload!)!.ReplyTo!,
            Payload = "{ \"myDummyValue\": 7 }"
        });
        var request = new DummyRequest { Value = "v" };

        var response = await sut.SendRequestAwaitFirstAnswer<DummyRequest, DummyResponse>("test", request);

        var sent = JsonSerializer.Deserialize<DummyRequest>(_published!.Payload!)!;
        Assert.Equal("v", sent.Value);
        Assert.NotNull(request.ReplyTo);
        Assert.Equal(request.ReplyTo, sent.ReplyTo);
        Assert.Equal(request.ReplyTo, _published.ReplyTo);
        Assert.Equal(7, response!.MyDummyValue);
    }

    [Fact]
    public async Task SendRequestAwaitFirstAnswer_ReturnsTheReplyPayload()
    {
        var sut = CreateSut(message => new Message { Topic = message.ReplyTo!, Payload = "text" });

        var payload = await sut.SendRequestAwaitFirstAnswer("test", new DummyRequest(), []);

        Assert.Equal("text", payload);
    }

    [Fact]
    public async Task SendRequestAwaitFirstAnswer_ReturnsNull_WhenTheRequestCannotBeSerialized()
    {
        var sut = CreateSut();

        var payload = await sut.SendRequestAwaitFirstAnswer("test", new UnserializableRequest(), []);

        Assert.Null(payload);
        Assert.Null(_published);
        _logger.AssertLogged(LogLevel.Error, m => m.Contains("test"));
    }

    [Fact]
    public async Task SendRequestAwaitFirstReply_RoundTripsBinaryPayloads()
    {
        byte[] requestBytes = [1, 2, 3];
        var sut = CreateSut(ReplyWithBinaryIfAccepted);

        var reply = await sut.SendRequestAwaitFirstReply("test", "meta", requestBytes,
            new RequestOptions { AcceptedReplyFormats = MessageFormats.Text | MessageFormats.Binary });

        Assert.Equal("meta", _published!.Payload);
        Assert.Same(requestBytes, _published.BinaryPayload);
        Assert.Equal(MessageFormats.Text | MessageFormats.Binary, _published.AcceptedReplyFormats);
        Assert.Same(ReplyBytes, reply!.BinaryPayload);
        Assert.Null(reply.Payload);
        _logger.AssertNotLogged(LogLevel.Warning);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("p", false)]
    [InlineData(null, true)]
    [InlineData("p", true)]
    public void SendRequestAwaitFirstReply_SendsThePayloadsAsGiven(string? payload, bool hasBinaryPayload)
    {
        byte[]? binaryPayload = hasBinaryPayload ? [1] : null;
        var sut = CreateSut();

        _ = sut.SendRequestAwaitFirstReply("test", payload, binaryPayload);

        Assert.Equal("test", _published!.Topic);
        Assert.Equal(payload, _published.Payload);
        Assert.Same(binaryPayload, _published.BinaryPayload);
        Assert.StartsWith("private/reply/", _published.ReplyTo);
        Assert.Null(_published.AcceptedReplyFormats);
    }

    [Theory]
    [InlineData(null, "private/reply/")]
    [InlineData("", "private/reply/")]
    [InlineData("my/prefix/", "my/prefix/")]
    public void SendRequestAwaitFirstReply_RepliesUnderTheReplyTopicPrefix(string? prefix, string expected)
    {
        var sut = CreateSut();

        _ = sut.SendRequestAwaitFirstReply("test", options: new RequestOptions { ReplyTopicPrefix = prefix });

        Assert.StartsWith(expected, _published!.ReplyTo);
    }

    [Fact]
    public void SendRequestAwaitFirstReply_RepliesUnderTheDefaultPrefix()
    {
        var sut = CreateSut();
        sut.SetDefaultPrefix("my/default/");

        _ = sut.SendRequestAwaitFirstReply("test");

        Assert.StartsWith("my/default/", _published!.ReplyTo);
    }

    [Fact]
    public async Task SendRequestAwaitFirstReply_ReturnsNullAfterTheTimeoutOfTheOptions()
    {
        var sut = CreateSut();

        var task = sut.SendRequestAwaitFirstReply("test", options: new RequestOptions { MillisecondsTimeoutTarget = 2000 });
        _heartbeat.Beat += Raise.EventWith(this, new HeartbeatEventArgs(1000, 1));
        var completedEarly = task.IsCompleted;
        _heartbeat.Beat += Raise.EventWith(this, new HeartbeatEventArgs(1000, 2));

        Assert.False(completedEarly);
        Assert.Null(await task);
    }

    [Fact]
    public async Task SendRequestAwaitFirstReply_ReturnsNullOnDisposal()
    {
        var sut = CreateSut();

        var task = sut.SendRequestAwaitFirstReply("test");
        sut.Dispose();

        Assert.Null(await task);
    }

    [Fact]
    public async Task SendRequestAwaitFirstReply_ReturnsNullAndClosesTheRequest_WhenPublishFails()
    {
        _messaging.When(m => m.Publish(Arg.Any<Message>())).Do(_ => throw new InvalidOperationException("publish failed"));
        var sut = new RequestClient.RequestClient(_messaging, _heartbeat, _logger);

        var reply = await sut.SendRequestAwaitFirstReply("test");
        sut.Dispose();

        Assert.Null(reply);
        _logger.AssertLogged(LogLevel.Error, m => m.Contains("test"));
        _logger.AssertNotLogged(m => m.Contains("on shutdown"));
    }

    [Fact]
    public async Task Handle_CompletesTheRequestWithTheFirstReplyOnly()
    {
        var sut = CreateSut();

        var task = sut.SendRequestAwaitFirstReply("test");
        var first = new Message { Topic = _published!.ReplyTo!, Payload = "1" };
        sut.Handle(first);
        sut.Handle(new Message { Topic = _published.ReplyTo!, Payload = "2" });

        Assert.Same(first, await task);
    }

    [Fact]
    public async Task Handle_WarnsAboutAReplyFormatTheRequestDoesNotAccept()
    {
        var sut = CreateSut(message => new Message { Topic = message.ReplyTo!, Payload = "{ \"myDummyValue\": 1 }", BinaryPayload = [1] });

        var response = await sut.SendRequestAwaitFirstAnswer<DummyRequest, DummyResponse>("test", new DummyRequest());

        Assert.Equal(1, response!.MyDummyValue);
        _logger.AssertLogged(LogLevel.Warning, m => m.Contains("test") && m.Contains("Text, Binary"));
    }

    [Fact]
    public async Task SendRequest_SubscribesTheReplyTopicsOfItsPrefixBeforePublishing()
    {
        Action<Message>? subscribed = null;
        _messaging.Subscribe(Arg.Any<string>(), Arg.Do<Action<Message>>(h => subscribed = h));
        var sut = CreateSut();

        var task = sut.SendRequestAwaitFirstReply("test");
        var reply = new Message { Topic = _published!.ReplyTo! };
        subscribed!(reply);

        Received.InOrder(() =>
        {
            _messaging.Subscribe(Arg.Is<string>(p => p.StartsWith("private/reply/*/requestclient/")), Arg.Any<Action<Message>>());
            _messaging.Publish(Arg.Any<Message>());
        });
        Assert.Same(reply, await task);
    }

    [Fact]
    public async Task SendRequest_UsesTheInjectedReplyTopics()
    {
        var replyTopics = Substitute.For<IReplyTopics>();
        replyTopics.Create("private/reply").Returns("reply/1");
        Action<Message>? handleReply = null;
        var sut = new RequestClient.RequestClient(_messaging, _heartbeat, _logger, h => { handleReply = h; return replyTopics; });
        _messaging.When(m => m.Publish(Arg.Any<Message>())).Do(ci => _published = ci.Arg<Message>());

        var task = sut.SendRequestAwaitFirstReply("test");
        var reply = new Message { Topic = "reply/1" };
        handleReply!(reply);
        sut.Dispose();

        Assert.Equal("reply/1", _published!.ReplyTo);
        Assert.Same(reply, await task);
        replyTopics.Received(1).Dispose();
    }

    [Fact]
    public async Task SendRequest_ReturnsNull_WhenTheReplyTopicCannotBeCreated()
    {
        var replyTopics = Substitute.For<IReplyTopics>();
        replyTopics.Create(Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("subscribe failed"));
        var sut = new RequestClient.RequestClient(_messaging, _heartbeat, _logger, _ => replyTopics);

        var reply = await sut.SendRequestAwaitFirstReply("test");

        Assert.Null(reply);
        _messaging.DidNotReceive().Publish(Arg.Any<Message>());
        _logger.AssertLogged(LogLevel.Error, m => m.Contains("test"));
    }

    private static readonly byte[] ReplyBytes = [4, 5];

    /// <summary>
    /// A responder as documented: binary only if the request accepts it, text otherwise.
    /// </summary>
    private static Message ReplyWithBinaryIfAccepted(Message message)
        => message.AcceptsReplyFormat(MessageFormats.Binary)
            ? new Message { Topic = message.ReplyTo!, BinaryPayload = ReplyBytes }
            : new Message { Topic = message.ReplyTo!, Payload = "{ \"myDummyValue\": 123 }" };

    private RequestClient.RequestClient CreateSut(Func<Message, Message>? responder = null)
    {
        var sut = new RequestClient.RequestClient(_messaging, _heartbeat, _logger);
        _messaging.When(m => m.Publish(Arg.Any<Message>())).Do(ci =>
        {
            _published = ci.Arg<Message>();
            if (responder != null) sut.Handle(responder(_published));
        });
        return sut;
    }

    private class DummyRequest : MessageRequestBase
    {
        public string? Value { get; set; }
    }

    private class UnserializableRequest : MessageRequestBase
    {
        public string Value => throw new InvalidOperationException("not serializable");
    }

    private class DummyResponse
    {
        public int MyDummyValue { get; set; }
    }
}


