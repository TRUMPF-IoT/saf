// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common.Tests;

using Xunit;

public class WaitUtilitiesTests
{
    [Fact]
    public async Task WaitUntil_WhenConditionEventuallyBecomesTrue_CompletesSuccessfully()
    {
        var completed = 0;

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);
            Interlocked.Exchange(ref completed, 1);
        }, TestContext.Current.CancellationToken);

        var exception = await Record.ExceptionAsync(async () => await WaitUtilities.WaitUntil(
            () => Volatile.Read(ref completed) == 1,
            frequency: TimeSpan.FromMilliseconds(5),
            timeout: TimeSpan.FromSeconds(2),
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(exception);
    }

    [Fact]
    public async Task WaitUntil_WhenConditionNeverBecomesTrue_ThrowsTimeoutException()
        => await Assert.ThrowsAsync<TimeoutException>(() =>
        WaitUtilities.WaitUntil(() => false, frequency: TimeSpan.FromMilliseconds(5), timeout: TimeSpan.FromMilliseconds(50), cancellationToken: TestContext.Current.CancellationToken));
}
