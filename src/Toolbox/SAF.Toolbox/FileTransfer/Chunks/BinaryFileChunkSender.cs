// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.RequestClient;
using SAF.Toolbox.Serialization;

namespace SAF.Toolbox.FileTransfer.Chunks;

/// <summary>
/// Sends the chunk data as binary payload and the file and chunk index as JSON payload,
/// which receivers on SAF 11.x read.
/// </summary>
internal sealed class BinaryFileChunkSender(IRequestClient requestClient, IMessagingInfrastructure messaging) : IFileChunkSender
{
    public MessageFormats Format => MessageFormats.Binary;

    public bool CanSend(string topic)
        => messaging.CanPublish(new Message { Topic = topic, Payload = string.Empty, BinaryPayload = [] });

    public async Task<FileReceiverStatus?> SendAsync(string topic, TransportFile file, FileChunk chunk, uint timeoutMs)
    {
        var reply = await requestClient.SendRequestAwaitFirstReply(topic,
            JsonSerializer.Serialize(new SendBinaryFileChunkRequest { File = file, Index = chunk.Index }),
            chunk.Data,
            new RequestOptions { ReplyTopicPrefix = RequestClientExtensions.ReplyToPrefix, MillisecondsTimeoutTarget = timeoutMs });

        return reply?.Payload is { } payload ? JsonSerializer.Deserialize<SendFileChunkResponse>(payload)?.Status : null;
    }
}
