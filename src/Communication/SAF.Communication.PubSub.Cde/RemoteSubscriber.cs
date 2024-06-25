// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Communication.PubSub.Cde;
using nsCDEngine.BaseClasses;
using SAF.Communication.Cde;
using SAF.Common;
using Interfaces;

/// <summary>
/// Contains the information about a subscriber running on another node.
/// These remote subscibers are manged by <see cref="SubscriptionRegistry"/>.
/// </summary>
internal class RemoteSubscriber : IRemoteSubscriber
{
    private readonly RegistrySubscriptionRequest _registryRequest;
    private readonly HashSet<string> _patterns;
    private DateTimeOffset _lastActivity = DateTimeOffset.UtcNow;

    public RemoteSubscriber(TSM tsm)
        : this(tsm, new List<string>(), new RegistrySubscriptionRequest())
    { }
    public RemoteSubscriber(TSM tsm, IList<string> patterns, RegistrySubscriptionRequest request)
    {
        Tsm = tsm;
        _registryRequest = request;
        _patterns = new HashSet<string>(patterns.Distinct());
        IsLocalHost = tsm.IsLocalHost();
    }

    public TSM Tsm { get; }
    public bool IsLocalHost { get; }
    public bool IsAlive => DateTimeOffset.UtcNow - _lastActivity <= TimeSpan.FromSeconds(Subscriber.AliveIntervalSeconds * 2);
    public bool IsRegistry => _registryRequest.isRegistry;
    public string TargetEngine => IsRegistry ? Engines.PubSub : Engines.RemotePubSub;

    public string Version => string.IsNullOrEmpty(_registryRequest.version)
        ? PubSubVersion.V1
        : _registryRequest.version;

    public void AddPatterns(IList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (_patterns.Contains(pattern)) continue;
            _patterns.Add(pattern);
        }
    }

    public void RemovePatterns(IList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            _patterns.Remove(pattern);
        }
    }

    public bool HasPatterns => _patterns.Count != 0;

    public bool IsMatch(string topic)
        => _patterns.Contains("*") || _patterns.Contains(topic) || _patterns.Any(topic.IsMatch);

    public void Touch() => _lastActivity = DateTimeOffset.UtcNow;
}

internal static class RemoteSubscriberExtensions
{
    public static bool IsRoutingAllowed(this IRemoteSubscriber subscriber, RoutingOptions routingOptions)
    {
        switch (routingOptions)
        {
            case RoutingOptions.All:
                return true;

            case RoutingOptions.Local:
                return subscriber.IsLocalHost;

            case RoutingOptions.Remote:
                return !subscriber.IsLocalHost;

            default:
                throw new ArgumentOutOfRangeException(nameof(routingOptions));
        }
    }
}