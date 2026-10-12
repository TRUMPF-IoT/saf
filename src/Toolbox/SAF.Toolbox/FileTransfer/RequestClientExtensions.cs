// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.RequestClient;

namespace SAF.Toolbox.FileTransfer;

internal static class RequestClientExtensions
{
    public const string ReplyToPrefix = "private/data/transfer/receipt";

    public static Task<GetReceiverStateResponse?> GetReceiverStateAsync(this IRequestClient requestClient, ILogger logger, string topic, TransportFile file, FileSenderOptions options)
        => Retry.UntilAsync(
            logger,
            () => requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                $"{topic}/state/get",
                new GetReceiverStateRequest { File = file },
                [], ReplyToPrefix),
            result => result != null,
            intervalFactory: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            retryAttempts: options.RetryAttemptsForFailedChunks);
}
