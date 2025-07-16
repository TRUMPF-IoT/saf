// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.RequestClient;

namespace SAF.Toolbox.FileTransfer;

internal static class RequestClientExtensions
{
    private const string ReplyToPrefix = "private/data/transfer/receipt";

    public static async Task<FileReceiverState?> GetReceiverStateAsync(this IRequestClient requestClient, string topic, TransportFile file, FileSenderOptions options)
    {
        var response = await RetryAsync(
            () => requestClient.SendRequestAwaitFirstAnswer<GetReceiverStateRequest, GetReceiverStateResponse>(
                $"{topic}/state/get",
                new GetReceiverStateRequest { File = file },
                [], ReplyToPrefix),
            result => result != null,
            intervalFactory: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            retryAttempts: options.RetryAttemptsForFailedChunks);

        return response?.State;
    }

    public static async Task<FileTransferStatus> SendFileChunkAsync(this IRequestClient requestClient, string topic, SendFileChunkRequest request, FileSenderOptions options, uint timeoutMs)
    {
        var response = await RetryAsync(
            () => requestClient.SendRequestAwaitFirstAnswer<SendFileChunkRequest, SendFileChunkResponse>(topic, request, [], ReplyToPrefix, timeoutMs),
            result => result is {Status: FileReceiverStatus.Ok},
            intervalFactory: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            retryAttempts: options.RetryAttemptsForFailedChunks);

        if (response == null)
        {
            return FileTransferStatus.TimedOut;
        }

        return response.Status != FileReceiverStatus.Ok ? FileTransferStatus.Error : FileTransferStatus.Delivered;
    }

    private static async Task<TResult> RetryAsync<TResult>(
        Func<Task<TResult>> action,
        Predicate<TResult> isDesiredResult,
        Func<int, TimeSpan> intervalFactory,
        int retryAttempts = 0)
    {
        // Perform the action once before retrying
        var result = await action();

        if (isDesiredResult(result))
        {
            return result;
        }

        for (var attempted = 0; attempted < retryAttempts; attempted++)
        {
            result = await action();

            if (isDesiredResult(result))
            {
                return result;
            }

            var retryInterval = intervalFactory(attempted);
            // Wait before retrying
            await Task.Delay(retryInterval);
        }

        // Return the last result, even if it is not the desired one
        return result;
    }
}