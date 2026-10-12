// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.FileTransfer;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.Messaging.Contracts;
using SAF.Messaging.InProcess;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.Heartbeat;
using SAF.Toolbox.Serialization;
using Testably.Abstractions.Testing;
using Xunit;
using HeartbeatImpl = SAF.Toolbox.Heartbeat.Heartbeat;
using RequestClientImpl = SAF.Toolbox.RequestClient.RequestClient;
using ToolboxServices = SAF.Toolbox.ServiceCollectionExtensions;

/// <summary>
/// File transfers between SAF 11.x and SAF 10.x nodes over the in-process transport. SAF 10.x nodes are emulated with
/// the payloads SAF 10.x reads and writes: the reply topic only in the JSON payload, the chunk data Base64-encoded in it.
/// </summary>
public sealed class FileTransferCompatibilityTests : IDisposable
{
    public enum Node { Saf10, Saf11 }

    private const string Topic = "transfer/firmware";
    private const uint ChunkSize = 1024;
    private const uint TotalChunks = 10;

    private readonly ServiceProvider _services;
    private readonly IMessagingInfrastructure _transport;
    private readonly HeartbeatImpl _heartbeat = new(100);
    private readonly MockFileSystem _senderFileSystem = new();
    private readonly MockFileSystem _receiverFileSystem = new();
    private readonly byte[] _content = new byte[ChunkSize * (TotalChunks - 1) + 100];
    private readonly List<IDisposable> _disposables = [];

    public FileTransferCompatibilityTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IServiceMessageDispatcher, HandlerDispatcher>()
            .AddInProcessMessagingInfrastructure()
            .BuildServiceProvider();
        _transport = _services.GetRequiredKeyedService<IMessagingInfrastructureFactory>(MessagingInfrastructureKeys.InProcess)
            .Create(new MessagingConfiguration());

        new Random(42).NextBytes(_content);
        _senderFileSystem.Initialize().WithFile("file.bin").Which(f => f.HasBytesContent(_content));
    }

    [Theory]
    [InlineData(Node.Saf11, Node.Saf11, true, true)]
    [InlineData(Node.Saf11, Node.Saf11, false, false)]
    [InlineData(Node.Saf11, Node.Saf10, true, false)]
    [InlineData(Node.Saf10, Node.Saf11, true, false)]
    [InlineData(Node.Saf10, Node.Saf10, true, false)]
    public async Task TransfersTheFile(Node sender, Node receiver, bool canPublishBinary, bool expectBinaryChunks)
    {
        var messaging = new RecordingMessaging(_transport, canPublishBinary);
        SubscribeReceiver(receiver, messaging);

        Assert.True(await SendAsync(sender, messaging));

        Assert.Equal(_content, _receiverFileSystem.File.ReadAllBytes(_receiverFileSystem.Path.Combine("received", "file.bin")));
        var chunkRequests = messaging.Published.Where(m => m.Topic == Topic).ToList();
        Assert.Equal((int)TotalChunks, chunkRequests.Count);
        Assert.All(chunkRequests, m => Assert.Equal(expectBinaryChunks, m.BinaryPayload != null));
    }

    [Fact]
    public async Task Saf11Receiver_RejectsAFileASaf10SenderResumedWithWrongData_AndGetsItWithTheNextAttempt()
    {
        var messaging = new RecordingMessaging(_transport, canPublishBinary: true);
        SubscribeReceiver(Node.Saf11, messaging);
        var target = _receiverFileSystem.Path.Combine("received", "file.bin");

        Assert.False(await SendAsSaf10Async(messaging, interruptAfter: 4));
        Assert.False(await SendAsSaf10Async(messaging));
        Assert.False(_receiverFileSystem.File.Exists(target));

        Assert.True(await SendAsSaf10Async(messaging));
        Assert.Equal(_content, _receiverFileSystem.File.ReadAllBytes(target));
    }

    private void SubscribeReceiver(Node node, IMessagingInfrastructure messaging)
    {
        var statefulReceiver = new StatefulFileReceiver(NullLogger<StatefulFileReceiver>.Instance, _receiverFileSystem,
            new FileReceiverOptions { StateExpirationAfterHours = 0 }, Substitute.For<IHeartbeatPool>(), "received");
        _disposables.Add(statefulReceiver);

        if (node == Node.Saf11)
        {
            ToolboxServices.CreateFileReceiver(NullLogger<FileReceiver>.Instance, messaging).Subscribe(Topic, statefulReceiver);
            return;
        }

        messaging.Subscribe($"{Topic}/state/get", message =>
        {
            var request = JsonSerializer.Deserialize<GetReceiverStateRequest>(message.Payload!)!;
            messaging.Publish(new Message
            {
                Topic = request.ReplyTo!,
                Payload = JsonSerializer.Serialize(new GetReceiverStateResponse { State = statefulReceiver.GetState(request.File) })
            });
        });
        messaging.Subscribe(Topic, message =>
        {
            var request = JsonSerializer.Deserialize<SendFileChunkRequest>(message.Payload!);
            if (request?.ReplyTo == null || request.FileChunk == null) return;

            messaging.Publish(new Message
            {
                Topic = request.ReplyTo,
                Payload = JsonSerializer.Serialize(new SendFileChunkResponse { Status = statefulReceiver.WriteFile(request.File, request.FileChunk) })
            });
        });
    }

    private async Task<bool> SendAsync(Node node, IMessagingInfrastructure messaging)
    {
        if (node == Node.Saf11)
        {
            var requestClient = new RequestClientImpl(messaging, _heartbeat, null);
            _disposables.Add(requestClient);
            var sender = ToolboxServices.CreateFileSender(NullLogger<FileSender>.Instance,
                Options.Create(new FileSenderOptions { MaxChunkSizeInBytes = ChunkSize }), _senderFileSystem, requestClient, messaging);
            return await sender.SendAsync(Topic, "file.bin", 5000) == FileTransferStatus.Delivered;
        }

        return await SendAsSaf10Async(messaging);
    }

    private async Task<bool> SendAsSaf10Async(IMessagingInfrastructure messaging, int? interruptAfter = null)
    {
        var fileInfo = _senderFileSystem.FileInfo.New("file.bin");
        var file = new TransportFile
        {
            FileName = "file.bin",
            FileId = fileInfo.GetFileId(ChunkSize),
            ContentHash = fileInfo.GetContentHash(),
            ContentLength = _content.Length,
            ChunkSize = ChunkSize,
            TotalChunks = TotalChunks
        };
        var state = await RequestAsSaf10<GetReceiverStateResponse>(messaging, $"{Topic}/state/get", new GetReceiverStateRequest { File = file });
        // Like SAF 10.x, read the file chunk by chunk without moving past the chunks the receiver already has.
        var chunksRead = 0;
        for (uint index = 0; index < TotalChunks; index++)
        {
            if (state.State.TransmittedChunks.Contains(index)) continue;
            if (chunksRead == interruptAfter) return false;

            var data = _content.Skip(chunksRead++ * (int)ChunkSize).Take((int)ChunkSize).ToArray();
            var response = await RequestAsSaf10<SendFileChunkResponse>(messaging, Topic,
                new SendFileChunkRequest { File = file, FileChunk = new FileChunk { Index = index, Data = data } });
            if (response.Status != FileReceiverStatus.Ok) return false;
        }

        return true;
    }

    private static async Task<TResponse> RequestAsSaf10<TResponse>(IMessagingInfrastructure messaging, string topic, MessageRequestBase request)
    {
        var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        request.ReplyTo = $"saf10/reply/{Guid.NewGuid()}";
        var subscription = messaging.Subscribe(request.ReplyTo, message => reply.TrySetResult(message.Payload!));
        try
        {
            messaging.Publish(new Message { Topic = topic, Payload = JsonSerializer.Serialize(request) });
            return JsonSerializer.Deserialize<TResponse>(await reply.Task.WaitAsync(TimeSpan.FromSeconds(5)))!;
        }
        finally
        {
            messaging.Unsubscribe(subscription);
        }
    }

    public void Dispose()
    {
        _disposables.ForEach(d => d.Dispose());
        _heartbeat.Dispose();
        _services.Dispose();
    }

    // Records what is published and answers CanPublish like a transport with binary payloads enabled or disabled.
    private sealed class RecordingMessaging(IMessagingInfrastructure transport, bool canPublishBinary) : IMessagingInfrastructure
    {
        public ConcurrentQueue<Message> Published { get; } = new();

        public void Publish(Message message)
        {
            Published.Enqueue(message);
            transport.Publish(message);
        }

        public bool CanPublish(Message message) => canPublishBinary || message.BinaryPayload is null;

        public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler => transport.Subscribe<TMessageHandler>();
        public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
            => transport.Subscribe<TMessageHandler>(routeFilterPattern);
        public object Subscribe(Action<Message> handler) => transport.Subscribe(handler);
        public object Subscribe(string routeFilterPattern, Action<Message> handler) => transport.Subscribe(routeFilterPattern, handler);
        public void Unsubscribe(object subscription) => transport.Unsubscribe(subscription);
    }

    private sealed class HandlerDispatcher : IServiceMessageDispatcher
    {
        public void DispatchMessage<TMessageHandler>(Message message) where TMessageHandler : IMessageHandler => throw new NotSupportedException();
        public void DispatchMessage(Type handlerType, Message message) => throw new NotSupportedException();
        public void DispatchMessage(Action<Message> handler, Message message) => handler(message);
    }
}
