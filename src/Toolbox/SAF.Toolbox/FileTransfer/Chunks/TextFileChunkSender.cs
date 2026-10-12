// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.RequestClient;

namespace SAF.Toolbox.FileTransfer.Chunks;

/// <summary>
/// Sends the chunk data Base64-encoded in the JSON payload, which receivers of every SAF version read.
/// </summary>
internal sealed class TextFileChunkSender(IRequestClient requestClient) : IFileChunkSender
{
    public MessageFormats Format => MessageFormats.Text;

    public bool CanSend(string topic) => true;

    public async Task<FileReceiverStatus?> SendAsync(string topic, TransportFile file, FileChunk chunk, uint timeoutMs)
    {
        var response = await requestClient.SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>(
            topic, new SendFileChunkRequest { File = file, FileChunk = chunk }, [], RequestClientExtensions.ReplyToPrefix, timeoutMs);
        return response?.Status;
    }
}
