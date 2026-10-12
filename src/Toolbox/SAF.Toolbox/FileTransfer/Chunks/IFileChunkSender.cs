// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Messaging.Contracts;

namespace SAF.Toolbox.FileTransfer.Chunks;

/// <summary>
/// Sends file chunks to a receiver in one payload format.
/// </summary>
internal interface IFileChunkSender
{
    /// <summary>
    /// Gets the payload format the chunk data travels in.
    /// </summary>
    MessageFormats Format { get; }

    /// <summary>
    /// Sends the chunk and returns the receiver's status, or <c>null</c> if no reply arrives in time.
    /// </summary>
    Task<FileReceiverStatus?> SendAsync(string topic, TransportFile file, FileChunk chunk, uint timeoutMs);
}
