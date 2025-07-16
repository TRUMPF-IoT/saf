using SAF.Common;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.Serialization;

namespace SAF.Toolbox.FileTransfer;

using Microsoft.Extensions.Logging;

internal class FileReceiver(ILogger<FileReceiver> log, IMessagingInfrastructure messaging) : IFileReceiver
{
    private class SubscriptionEntry
    {
        public required object SendFileChunk { get; init; }
        public required object GetReceiverState { get; init; }
    }

    private readonly Dictionary<string, SubscriptionEntry> _subscriptions = [];

    public void Subscribe(string topic, IStatefulFileReceiver statefulFileReceiver, string folderPath)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(statefulFileReceiver);

        lock (_subscriptions)
        {
            if (_subscriptions.ContainsKey(topic))
            {
                throw new ArgumentException($"An element with the same key already exists: {topic}");
            }

            log.LogDebug("Subscribing stateful receiver on {ReceiverTopic} for folder {ReceiverFolderPath}", topic, folderPath);
            _subscriptions.Add(topic,
                new SubscriptionEntry
                {
                    GetReceiverState = messaging.Subscribe($"{topic}/state/get", message => HandleGetReceiverState(message, statefulFileReceiver, folderPath)),
                    SendFileChunk = messaging.Subscribe(topic, message => HandleSendFileChunks(message, statefulFileReceiver, folderPath))
                });
        }
    }

    public void Unsubscribe(string topic)
    {
        lock (_subscriptions)
        {
            if (!_subscriptions.TryGetValue(topic, out var subscription)) return;

            messaging.Unsubscribe(subscription.GetReceiverState);
            messaging.Unsubscribe(subscription.SendFileChunk);

            _subscriptions.Remove(topic);
        }
    }

    public void Unsubscribe()
    {
        lock (_subscriptions)
        {
            foreach (var subscription in _subscriptions.Values)
            {
                messaging.Unsubscribe(subscription.GetReceiverState);
                messaging.Unsubscribe(subscription.SendFileChunk);
            }

            _subscriptions.Clear();
        }
    }

    private void HandleGetReceiverState(Message message, IStatefulFileReceiver statefulFileReceiver, string folderPath)
    {
        if (message.Payload == null)
        {
            log.LogWarning("Missing payload in {MethodName}", nameof(HandleGetReceiverState));
            return;
        }

        var request = JsonSerializer.Deserialize<GetReceiverStateRequest>(message.Payload);
        if (request?.ReplyTo == null) return;

        var receiverState = statefulFileReceiver.GetState(folderPath, request.File);
        
        messaging.Publish(new Message
        {
            Topic = request.ReplyTo,
            Payload = JsonSerializer.Serialize(new GetReceiverStateResponse { State = receiverState })
        });
    }

    private void HandleSendFileChunks(Message message, IStatefulFileReceiver statefulFileReceiver, string folderPath)
    {
        if (message.Payload == null)
        {
            log.LogWarning("Missing payload in {MethodName}", nameof(HandleSendFileChunks));
            return;
        }

        var request = JsonSerializer.Deserialize<SendFileChunkRequest>(message.Payload);
        if (request?.ReplyTo == null || request.FileChunk == null) return;

        var receiverStatus = statefulFileReceiver.WriteFile(folderPath, request.File, request.FileChunk);
        if(receiverStatus != FileReceiverStatus.Ok)
        {
            log.LogWarning("Failed to write file chunk {ChunkIndex} of file {FileName}. Status: {Status}",
                request.FileChunk.Index, request.File.FileName, receiverStatus);
        }

        messaging.Publish(new Message
        {
            Topic = request.ReplyTo,
            Payload = JsonSerializer.Serialize(new SendFileChunkResponse { Status = receiverStatus })
        });
    }
}