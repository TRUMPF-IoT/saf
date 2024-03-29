// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;
using SAF.Communication.PubSub.Interfaces;

namespace SAF.Communication.PubSub;

internal struct RemoteRegistry<TMessage>
{
    public TMessage Tsm { get; set; }
    public string? InstanceId { get; set; }
    public DateTimeOffset LastActivity { get; set; }
}

public class RegistryLifetimeHandlerBase<TMessage> : IRegistryLifetimeHandler<TMessage>, IDisposable
{
    private readonly ConcurrentDictionary<string, RemoteRegistry<TMessage>> _knownRegistries = new();

    private readonly Timer _registryLifetimeCheckTimer;
    private int _checkingLifetime;
    private readonly int _aliveIntervalSeconds;

    public event Action<TMessage, string>? RegistryUp;
    public event Action<TMessage>? RegistryDown;

    /// <summary>
    /// List with all nodes (each represented by a TSM) where the associated subscriber is registered.
    /// </summary>
    public IList<TMessage> Registries => _knownRegistries.Values.Select(r => r.Tsm).ToList();

    protected RegistryLifetimeHandlerBase(int aliveIntervalSeconds)
    {
        _aliveIntervalSeconds = aliveIntervalSeconds;
        _registryLifetimeCheckTimer = new Timer(OnRegistryLifetimeCheckTimer, null,
            TimeSpan.FromSeconds(_aliveIntervalSeconds * 2),
            TimeSpan.FromSeconds(_aliveIntervalSeconds * 2));
    }

    protected void HandleMessage(string registry, string messageToken, TMessage tsm)
    {
        HandleMessage(registry, null, messageToken, tsm);
    }

    protected void HandleMessage(string registry, string? registryInstanceId, string messageToken, TMessage tsm)
    {
        if (messageToken.StartsWith(MessageToken.RegistryAlive) ||
            messageToken.StartsWith(MessageToken.SubscriberAlive) && !string.IsNullOrWhiteSpace(registryInstanceId) ||
            messageToken.StartsWith(MessageToken.DiscoveryResponse) ||
            messageToken.StartsWith(MessageToken.SubscribeResponse) ||
            messageToken.StartsWith(MessageToken.SubscribeTrigger))
        {
            UpdateRegistries(registry, registryInstanceId, messageToken, tsm);
        }
        else if(messageToken.StartsWith(MessageToken.RegistryShutdown))
        {
            HandleRegistryShutdown(registry, tsm);
        }
    }

    private void HandleRegistryShutdown(string registry, TMessage tsm)
    {
        if(_knownRegistries.TryRemove(registry, out var _))
            RegistryDown?.Invoke(tsm);
    }

    private void UpdateRegistries(string registry, string? registryInstanceId, string messageToken, TMessage tsm)
    {
        var registryUp = true;
        _ = _knownRegistries.AddOrUpdate(registry,
            _ => new RemoteRegistry<TMessage> { Tsm = tsm, InstanceId = registryInstanceId, LastActivity = DateTimeOffset.UtcNow },
            (_, value) =>
            {
                // instance id differs -> registry was down in between
                registryUp = value.InstanceId != registryInstanceId;
                value.InstanceId = registryInstanceId;
                value.LastActivity = DateTimeOffset.UtcNow;
                return value;
            });

        if (registryUp && !messageToken.StartsWith(MessageToken.SubscribeTrigger))
        {
            RegistryUp?.Invoke(tsm, messageToken);
        }
    }

    private void OnRegistryLifetimeCheckTimer(object? state)
    {
        if (Interlocked.Exchange(ref _checkingLifetime, 1) == 1) return;
        try
        {
            var toDelete = _knownRegistries
                .Where(r => DateTimeOffset.UtcNow - r.Value.LastActivity > TimeSpan.FromSeconds(_aliveIntervalSeconds * 2))
                .Select(r => r.Key)
                .ToArray();
            foreach (var reg in toDelete)
            {
                if(_knownRegistries.TryRemove(reg, out var msg))
                    RegistryDown?.Invoke(msg.Tsm);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _checkingLifetime, 0);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        _registryLifetimeCheckTimer.Dispose();
    }
}