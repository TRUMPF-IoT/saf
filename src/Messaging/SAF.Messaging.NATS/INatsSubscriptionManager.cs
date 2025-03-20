namespace SAF.Messaging.Nats;

public interface INatsSubscriptionManager
{
    bool TryAdd(Guid subscriptionId, (string routeFilterPattern, CancellationTokenSource cancellationTokenSource, Task) subscription);
    bool TryRemove(Guid subscriptionId, out (string routeFilterPattern, CancellationTokenSource cancellationTokenSource, Task) subscription);
}
