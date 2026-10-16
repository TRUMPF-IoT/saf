// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Logging;

namespace SAF.Toolbox.FileTransfer;

internal static class Retry
{
    public static async Task<TResult> UntilAsync<TResult>(
        ILogger logger,
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

        logger.LogDebug("Start retrying FileTransfer request");
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
