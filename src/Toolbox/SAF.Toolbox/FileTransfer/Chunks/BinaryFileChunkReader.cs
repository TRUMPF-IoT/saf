// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.Serialization;

namespace SAF.Toolbox.FileTransfer.Chunks;

/// <summary>
/// Reads chunk requests that carry the chunk data as binary payload, as senders on SAF 11.x send them.
/// </summary>
internal sealed class BinaryFileChunkReader : IFileChunkReader
{
    public MessageFormats Format => MessageFormats.Binary;

    public bool CanRead(Message message) => message.BinaryPayload is not null;

    public ReceivedFileChunk? Read(Message message)
    {
        if (message is not { Payload: { } payload, BinaryPayload: { } data, ReplyTo: { } replyTo }) return null;

        var request = JsonSerializer.Deserialize<SendBinaryFileChunkRequest>(payload);
        return request is null ? null : new ReceivedFileChunk(request.File, new FileChunk { Index = request.Index, Data = data }, replyTo);
    }
}
