// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.FileTransfer;

using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.FileTransfer.Chunks;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.Serialization;
using SAF.Messaging.Contracts;
using Xunit;

public class FileReceiverTests
{
    private readonly ILogger<FileReceiver> _logger;
    private readonly IMessagingInfrastructure _messaging = Substitute.For<IMessagingInfrastructure>();
    private readonly IStatefulFileReceiver _statefulReceiver = Substitute.For<IStatefulFileReceiver>();

    public FileReceiverTests(ITestOutputHelper outputHelper)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Warning));
        _logger = loggerFactory.CreateLogger<FileReceiver>();
    }

    [Fact]
    public void Subscribe_ThrowsArgumentNullException_WhenTopicIsNull()
    {
        var receiver = ServiceCollectionExtensions.CreateFileReceiver(_logger, _messaging);
        Assert.Throws<ArgumentNullException>(() => receiver.Subscribe(null!, _statefulReceiver));
    }

    [Fact]
    public void Subscribe_ThrowsArgumentNullException_WhenReceiverIsNull()
    {
        var receiver = ServiceCollectionExtensions.CreateFileReceiver(_logger, _messaging);
        Assert.Throws<ArgumentNullException>(() => receiver.Subscribe("topic", null!));
    }

    [Fact]
    public void Subscribe_ThrowsArgumentException_WhenTopicAlreadySubscribed()
    {
        var receiver = ServiceCollectionExtensions.CreateFileReceiver(_logger, _messaging);
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(new object());
        receiver.Subscribe("topic", _statefulReceiver);
        Assert.Throws<ArgumentException>(() => receiver.Subscribe("topic", _statefulReceiver));
    }

    [Fact]
    public void Unsubscribe_RemovesSubscription()
    {
        var receiver = ServiceCollectionExtensions.CreateFileReceiver(_logger, _messaging);
        var sub1 = new object();
        var sub2 = new object();
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(sub1, sub2);

        receiver.Subscribe("topic", _statefulReceiver);
        receiver.Unsubscribe("topic");

        _messaging.Received(1).Subscribe("topic/state/get", Arg.Any<Action<Message>>());
        _messaging.Received(1).Subscribe("topic", Arg.Any<Action<Message>>());
        _messaging.Received().Unsubscribe(sub1);
        _messaging.Received().Unsubscribe(sub2);
    }

    [Fact]
    public void UnsubscribeAll_RemovesAllSubscriptions()
    {
        var receiver = ServiceCollectionExtensions.CreateFileReceiver(_logger, _messaging);
        var sub1 = new object();
        var sub2 = new object();
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(sub1, sub2, sub1, sub2);

        receiver.Subscribe("topic1", _statefulReceiver);
        receiver.Subscribe("topic2", _statefulReceiver);
        receiver.Unsubscribe();

        _messaging.Received(1).Subscribe("topic1/state/get", Arg.Any<Action<Message>>());
        _messaging.Received(1).Subscribe("topic1", Arg.Any<Action<Message>>());
        _messaging.Received(1).Subscribe("topic2/state/get", Arg.Any<Action<Message>>());
        _messaging.Received(1).Subscribe("topic2", Arg.Any<Action<Message>>());
        _messaging.Received(4).Unsubscribe(Arg.Any<object>());
    }

    [Fact]
    public void HandleGetReceiverState_PublishesState_WhenPayloadIsValid()
    {
        const string topic = "topic";
        const string replyTo = "reply";

        var file = new TransportFile { FileName = "file.txt", FileId = "fileId", ContentHash = "contentHash", ChunkSize = 32, ContentLength = 32, TotalChunks = 1 };
        var state = new FileReceiverState();
        _statefulReceiver.GetState(Arg.Any<TransportFile>()).Returns(state);

        var request = new GetReceiverStateRequest { File = file, ReplyTo = replyTo };
        var message = new Message
        {
            Payload = JsonSerializer.Serialize(request)
        };

        Action<Message>? getStateHandler = null;
        _messaging.Subscribe($"{topic}/state/get", Arg.Any<Action<Message>>())
            .Returns(ci =>
            {
                getStateHandler = ci.Arg<Action<Message>>();
                return new object();
            });

        var receiver = ServiceCollectionExtensions.CreateFileReceiver(_logger, _messaging);
        receiver.Subscribe(topic, _statefulReceiver);

        Assert.NotNull(getStateHandler);
        getStateHandler.Invoke(message);

        _messaging.Received().Publish(Arg.Is<Message>(m =>
            m.Topic == replyTo &&
            m.Payload != null &&
            m.Payload == JsonSerializer.Serialize(new GetReceiverStateResponse { State = state, AcceptedChunkFormats = MessageFormats.Text | MessageFormats.Binary })
        ));
    }

    [Fact]
    public void HandleGetReceiverState_DoesNothing_WhenPayloadIsNull()
    {
        const string topic = "topic";

        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(new object());

        Action<Message>? getStateHandler = null;
        _messaging.Subscribe($"{topic}/state/get", Arg.Any<Action<Message>>())
            .Returns(ci =>
            {
                getStateHandler = ci.Arg<Action<Message>>();
                return new object();
            });

        var receiver = ServiceCollectionExtensions.CreateFileReceiver(_logger, _messaging);
        receiver.Subscribe(topic, _statefulReceiver);

        Assert.NotNull(getStateHandler);
        var message = new Message { Payload = null };
        getStateHandler.Invoke(message);

        _messaging.DidNotReceive().Publish(Arg.Any<Message>());
    }

    [Fact]
    public void HandleSendFileChunks_PublishesStatus_WhenPayloadIsValid()
    {
        const string topic = "topic";
        const string replyTo = "reply";

        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(new object());

        const FileReceiverStatus status = FileReceiverStatus.Failed;

        var file = new TransportFile { FileName = "file.txt", FileId = "fileId", ContentHash = "contentHash", ChunkSize = 32, ContentLength = 32, TotalChunks = 1 };
        _statefulReceiver.WriteFile(Arg.Any<TransportFile>(), Arg.Any<FileChunk>()).Returns(status);

        var request = new SendFileChunkRequest { File = file, FileChunk = new FileChunk { Index = 1, Data = new byte[2] }, ReplyTo = replyTo };
        var message = new Message
        {
            Payload = JsonSerializer.Serialize(request)
        };

        Action<Message>? sendFileChunkHandler = null;
        _messaging.Subscribe($"{topic}", Arg.Any<Action<Message>>())
            .Returns(ci =>
            {
                sendFileChunkHandler = ci.Arg<Action<Message>>();
                return new object();
            });

        var receiver = ServiceCollectionExtensions.CreateFileReceiver(_logger, _messaging);
        receiver.Subscribe(topic, _statefulReceiver);

        Assert.NotNull(sendFileChunkHandler);
        sendFileChunkHandler.Invoke(message);

        _messaging.Received().Publish(Arg.Is<Message>(m =>
            m.Topic == replyTo &&
            m.Payload != null &&
            m.Payload == JsonSerializer.Serialize(new SendFileChunkResponse { Status = status })
        ));
    }

    [Fact]
    public void HandleSendFileChunks_DoesNothing_WhenPayloadIsNull()
    {
        const string topic = "topic";

        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(new object());

        Action<Message>? sendFileChunkHandler = null;
        _messaging.Subscribe($"{topic}", Arg.Any<Action<Message>>())
            .Returns(ci =>
            {
                sendFileChunkHandler = ci.Arg<Action<Message>>();
                return new object();
            });

        var receiver = ServiceCollectionExtensions.CreateFileReceiver(_logger, _messaging);
        receiver.Subscribe(topic, _statefulReceiver);

        Assert.NotNull(sendFileChunkHandler);
        var message = new Message { Payload = null };
        sendFileChunkHandler.Invoke(message);

        _messaging.DidNotReceive().Publish(Arg.Any<Message>());
    }

    [Fact]
    public void HandleGetReceiverState_AnnouncesTheFormatsOfItsChunkReaders()
    {
        var reader = Substitute.For<IFileChunkReader>();
        reader.Format.Returns(MessageFormats.Text);
        _statefulReceiver.GetState(Arg.Any<TransportFile>()).Returns(new FileReceiverState());
        var handler = CaptureHandler("topic/state/get", new FileReceiver(_logger, _messaging, [reader]));

        handler(new Message { Topic = "topic/state/get", Payload = FileTransferGolden.StateRequest });

        _messaging.Received(1).Publish(Arg.Is<Message>(m => m.Topic == "reply/1" && m.Payload!.EndsWith(",\"acceptedChunkFormats\":1}")));
    }

    [Fact]
    public void HandleGetReceiverState_PrefersTheReplyTopicOfTheMessage()
    {
        _statefulReceiver.GetState(Arg.Any<TransportFile>()).Returns(new FileReceiverState());
        var handler = CaptureHandler("topic/state/get", ServiceCollectionExtensions.CreateFileReceiver(_logger, _messaging));

        handler(new Message { Topic = "topic/state/get", Payload = FileTransferGolden.StateRequest, ReplyTo = "reply/2" });

        _messaging.Received(1).Publish(Arg.Is<Message>(m => m.Topic == "reply/2"));
    }

    [Fact]
    public void HandleSendFileChunks_WritesTheChunkOfTheReaderThatCanReadTheMessage()
    {
        var message = new Message { Topic = "topic" };
        var request = new ReceivedFileChunk(FileTransferGolden.File, new FileChunk { Index = 1, Data = [1] }, "reply/1");
        var other = Substitute.For<IFileChunkReader>();
        var reader = Substitute.For<IFileChunkReader>();
        reader.CanRead(message).Returns(true);
        reader.Read(message).Returns(request);
        _statefulReceiver.WriteFile(request.File, request.Chunk).Returns(FileReceiverStatus.Ok);
        var handler = CaptureHandler("topic", new FileReceiver(_logger, _messaging, [other, reader]));

        handler(message);

        other.DidNotReceive().Read(Arg.Any<Message>());
        _statefulReceiver.Received(1).WriteFile(request.File, request.Chunk);
        _messaging.Received(1).Publish(Arg.Is<Message>(m => m.Topic == "reply/1" && m.Payload == FileTransferGolden.ChunkResponse));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HandleSendFileChunks_DropsAMessageNoReaderReads(bool canRead)
    {
        var reader = Substitute.For<IFileChunkReader>();
        reader.CanRead(Arg.Any<Message>()).Returns(canRead);
        reader.Read(Arg.Any<Message>()).Returns((ReceivedFileChunk?)null);
        var handler = CaptureHandler("topic", new FileReceiver(_logger, _messaging, [reader]));

        handler(new Message { Topic = "topic", Payload = "{}" });

        _statefulReceiver.DidNotReceiveWithAnyArgs().WriteFile(default!, default!);
        _messaging.DidNotReceive().Publish(Arg.Any<Message>());
    }

    private Action<Message> CaptureHandler(string topic, FileReceiver receiver)
    {
        Action<Message>? handler = null;
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(ci =>
        {
            if (ci.ArgAt<string>(0) == topic) handler = ci.ArgAt<Action<Message>>(1);
            return new object();
        });

        receiver.Subscribe("topic", _statefulReceiver);

        Assert.NotNull(handler);
        return handler;
    }
}


