// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.Serialization;

namespace SAF.Toolbox.FileTransfer.Chunks;

/// <summary>
/// Reads chunk requests that carry the chunk data Base64-encoded in the JSON payload, as senders of every SAF version send them.
/// </summary>
internal sealed class TextFileChunkReader : IFileChunkReader
{
    public MessageFormats Format => MessageFormats.Text;

    public bool CanRead(Message message) => message.BinaryPayload is null;

    public ReceivedFileChunk? Read(Message message)
    {
        if (message.Payload is null) return null;

        var request = JsonSerializer.Deserialize<SendFileChunkRequest>(message.Payload);
        // SAF 10.x senders carry the reply topic only in the payload.
        var replyTo = message.ReplyTo ?? request?.ReplyTo;
        return request?.FileChunk is null || replyTo is null ? null : new ReceivedFileChunk(request.File, request.FileChunk, replyTo);
    }
}
