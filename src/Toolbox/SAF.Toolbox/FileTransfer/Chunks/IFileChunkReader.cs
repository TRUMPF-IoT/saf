// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Messaging.Contracts;

namespace SAF.Toolbox.FileTransfer.Chunks;

/// <summary>
/// Reads file chunk requests of one payload format.
/// </summary>
internal interface IFileChunkReader
{
    /// <summary>
    /// Gets the payload format the chunk data travels in.
    /// </summary>
    MessageFormats Format { get; }

    /// <summary>
    /// Returns whether the message is a chunk request in this format.
    /// </summary>
    bool CanRead(Message message);

    /// <summary>
    /// Reads the chunk request, or returns <c>null</c> if it lacks the chunk or the reply topic.
    /// </summary>
    ReceivedFileChunk? Read(Message message);
}

internal sealed record ReceivedFileChunk(TransportFile File, FileChunk Chunk, string ReplyTo);
