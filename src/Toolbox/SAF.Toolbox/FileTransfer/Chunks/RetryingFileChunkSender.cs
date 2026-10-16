// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using SAF.Messaging.Contracts;

namespace SAF.Toolbox.FileTransfer.Chunks;

/// <summary>
/// Sends a chunk again while the receiver does not confirm it, up to the given number of retries.
/// </summary>
internal sealed class RetryingFileChunkSender(IFileChunkSender sender, int retryAttempts, Func<int, TimeSpan> retryInterval, ILogger logger)
    : IFileChunkSender
{
    public MessageFormats Format => sender.Format;

    public Task<FileReceiverStatus?> SendAsync(string topic, TransportFile file, FileChunk chunk, uint timeoutMs)
        => Retry.UntilAsync(logger, () => sender.SendAsync(topic, file, chunk, timeoutMs),
            status => status == FileReceiverStatus.Ok, retryInterval, retryAttempts);
}
