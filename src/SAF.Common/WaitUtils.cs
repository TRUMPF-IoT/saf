namespace SAF.Common;

public static class WaitUtils
{
    public static async Task WaitUntil(Func<bool> condition, CancellationToken cancellationToken = default, TimeSpan frequency = default, TimeSpan timeout = default)
    {
        const int DEFAULT_FREQUENCY_IN_MILLISECONDS = 25;
        const int DEFAULT_TIMEOUT_IN_SECONDS = 30;

        var currentFrequency = frequency == TimeSpan.Zero ? TimeSpan.FromMilliseconds(DEFAULT_FREQUENCY_IN_MILLISECONDS) : frequency;
        var currentTimeout = timeout == TimeSpan.Zero ? TimeSpan.FromSeconds(DEFAULT_TIMEOUT_IN_SECONDS) : timeout;

        var waitTask = Task.Run(async () =>
        {
            while (!condition()) await Task.Delay(currentFrequency, cancellationToken);
        }, cancellationToken);

        if (waitTask != await Task.WhenAny(waitTask, Task.Delay(currentTimeout, cancellationToken)))
        {
            throw new TimeoutException();
        }
    }
}
