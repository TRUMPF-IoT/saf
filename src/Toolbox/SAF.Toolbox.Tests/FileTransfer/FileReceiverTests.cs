// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.FileTransfer;

using Microsoft.Extensions.Logging;
using NSubstitute;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.Serialization;
using Common;
using Xunit;
using Xunit.Abstractions;

public class FileReceiverTests
{
    private readonly ILogger<FileReceiver> _logger;
    private readonly IMessagingInfrastructure _messaging = Substitute.For<IMessagingInfrastructure>();
    private readonly IStatefulFileReceiver _statefulReceiver = Substitute.For<IStatefulFileReceiver>();

    public FileReceiverTests(ITestOutputHelper outputHelper)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddXunit(outputHelper, LogLevel.Trace).SetMinimumLevel(LogLevel.Warning));
        _logger = loggerFactory.CreateLogger<FileReceiver>();
    }

    [Fact]
    public void Subscribe_ThrowsArgumentNullException_WhenTopicIsNull()
    {
        var receiver = new FileReceiver(_logger, _messaging);
        Assert.Throws<ArgumentNullException>(() => receiver.Subscribe(null!, _statefulReceiver, "folder"));
    }

    [Fact]
    public void Subscribe_ThrowsArgumentNullException_WhenReceiverIsNull()
    {
        var receiver = new FileReceiver(_logger, _messaging);
        Assert.Throws<ArgumentNullException>(() => receiver.Subscribe("topic", null!, "folder"));
    }

    [Fact]
    public void Subscribe_ThrowsArgumentException_WhenTopicAlreadySubscribed()
    {
        var receiver = new FileReceiver(_logger, _messaging);
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(new object());
        receiver.Subscribe("topic", _statefulReceiver, "folder");
        Assert.Throws<ArgumentException>(() => receiver.Subscribe("topic", _statefulReceiver, "folder"));
    }

    [Fact]
    public void Unsubscribe_RemovesSubscription()
    {
        var receiver = new FileReceiver(_logger, _messaging);
        var sub1 = new object();
        var sub2 = new object();
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(sub1, sub2);

        receiver.Subscribe("topic", _statefulReceiver, "folder");
        receiver.Unsubscribe("topic");

        _messaging.Received(1).Subscribe("topic/state/get", Arg.Any<Action<Message>>());
        _messaging.Received(1).Subscribe("topic", Arg.Any<Action<Message>>());
        _messaging.Received().Unsubscribe(sub1);
        _messaging.Received().Unsubscribe(sub2);
    }

    [Fact]
    public void UnsubscribeAll_RemovesAllSubscriptions()
    {
        var receiver = new FileReceiver(_logger, _messaging);
        var sub1 = new object();
        var sub2 = new object();
        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(sub1, sub2, sub1, sub2);

        receiver.Subscribe("topic1", _statefulReceiver, "folder1");
        receiver.Subscribe("topic2", _statefulReceiver, "folder2");
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
        const string folder = "folder";
        const string replyTo = "reply";

        var file = new TransportFile { FileName = "file.txt", FileId = "fileId", ContentHash = "contentHash", ChunkSize = 32, ContentLength = 32, TotalChunks = 1 };
        var state = new FileReceiverState();
        _statefulReceiver.GetState(folder, Arg.Any<TransportFile>()).Returns(state);

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

        var receiver = new FileReceiver(_logger, _messaging);
        receiver.Subscribe(topic, _statefulReceiver, folder);

        Assert.NotNull(getStateHandler);
        getStateHandler.Invoke(message);

        _messaging.Received().Publish(Arg.Is<Message>(m =>
            m.Topic == replyTo &&
            m.Payload != null &&
            m.Payload == JsonSerializer.Serialize(new GetReceiverStateResponse { State = state })
        ));
    }

    [Fact]
    public void HandleGetReceiverState_DoesNothing_WhenPayloadIsNull()
    {
        const string topic = "topic";
        const string folder = "folder";

        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(new object());

        Action<Message>? getStateHandler = null;
        _messaging.Subscribe($"{topic}/state/get", Arg.Any<Action<Message>>())
            .Returns(ci =>
            {
                getStateHandler = ci.Arg<Action<Message>>();
                return new object();
            });

        var receiver = new FileReceiver(_logger, _messaging);
        receiver.Subscribe(topic, _statefulReceiver, folder);

        Assert.NotNull(getStateHandler);
        var message = new Message { Payload = null };
        getStateHandler.Invoke(message);

        _messaging.DidNotReceive().Publish(Arg.Any<Message>());
    }

    [Fact]
    public void HandleSendFileChunks_PublishesStatus_WhenPayloadIsValid()
    {
        const string topic = "topic";
        const string folder = "folder";
        const string replyTo = "reply";

        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(new object());

        const FileReceiverStatus status = FileReceiverStatus.Failed;

        var file = new TransportFile { FileName = "file.txt", FileId = "fileId", ContentHash = "contentHash", ChunkSize = 32, ContentLength = 32, TotalChunks = 1 };
        _statefulReceiver.WriteFile(folder, Arg.Any<TransportFile>(), Arg.Any<FileChunk>()).Returns(status);

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

        var receiver = new FileReceiver(_logger, _messaging);
        receiver.Subscribe(topic, _statefulReceiver, folder);

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
        const string folder = "folder";

        _messaging.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()).Returns(new object());

        Action<Message>? sendFileChunkHandler = null;
        _messaging.Subscribe($"{topic}", Arg.Any<Action<Message>>())
            .Returns(ci =>
            {
                sendFileChunkHandler = ci.Arg<Action<Message>>();
                return new object();
            });

        var receiver = new FileReceiver(_logger, _messaging);
        receiver.Subscribe(topic, _statefulReceiver, folder);

        Assert.NotNull(sendFileChunkHandler);
        var message = new Message { Payload = null };
        sendFileChunkHandler.Invoke(message);

        _messaging.DidNotReceive().Publish(Arg.Any<Message>());
    }
}