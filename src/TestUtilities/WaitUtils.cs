// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace TestUtilities;
using System.Diagnostics;

public static class WaitUtils
{
    public static Task WaitUntil(Func<bool> condition)
        => WaitUntil(condition, TimeSpan.FromSeconds(5));

    public static async Task WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var sw = new Stopwatch();
        sw.Start();

        while(!condition())
        {
            var wait = (int) Math.Min(200, timeout.Subtract(sw.Elapsed).TotalMilliseconds);
            await Task.Delay(wait);

            if (sw.Elapsed > timeout)
                throw new TimeoutException($"WaitUntil timed out after {timeout.TotalMilliseconds}ms.");
        }
    }
}