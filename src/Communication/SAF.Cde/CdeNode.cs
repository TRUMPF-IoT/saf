// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Cde;
using Microsoft.Extensions.Logging;

/// <summary>
/// Counts the leases on the C-DEngine node: the first lease starts the node, the last one shuts it down.
/// </summary>
/// <remarks>
/// C-DEngine keeps its state in static fields, so there can only be one node per loaded copy of C-DEngine.
/// C-DEngine also cannot be started again in the same process, so a node that was shut down stays down and
/// every later <see cref="Acquire"/> fails.
/// </remarks>
internal sealed class CdeNode(
    ICdeRuntimeFactory runtimeFactory,
    ICdeNodeOwnership ownership,
    IEqualityComparer<CdeConfiguration> configurationComparer)
    : ICdeNode
{
    private readonly Lock _sync = new();
    private IDisposable? _runtime;
    private CdeConfiguration? _runtimeConfiguration;
    private int _leaseCount;
    private bool _shutDown;

    /// <summary>
    /// The node of this copy of SAF.Cde. Plug-in containers are built independently of each other, so this
    /// anchor is the one place they can share the node through; it is also the only static state.
    /// </summary>
    public static ICdeNode Shared { get; } = new CdeNode(
        new CdeApplicationFactory(),
        new AppDomainCdeNodeOwnership(AppDomainCdeNodeOwnership.DefaultMarkerKey),
        new CdeConfigurationComparer());

    public CdeNodeLease Acquire(CdeConfiguration configuration, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var logger = loggerFactory.CreateLogger<CdeNode>();

        lock (_sync)
        {
            if (_shutDown)
            {
                throw new InvalidOperationException(
                    "C-DEngine has already been shut down in this process and cannot be started again. A live reload " +
                    "of the plugin system (IPluginSystemController.ReloadAsync) is not supported while C-DEngine " +
                    "plug-ins are loaded; restart the host instead.");
            }

            if (_runtime is null)
            {
                ownership.Claim();
                _runtime = runtimeFactory.Start(configuration, loggerFactory);
                _runtimeConfiguration = configuration;
            }
            else if (!configurationComparer.Equals(configuration, _runtimeConfiguration))
            {
                logger.LogWarning(
                    "C-DEngine is already running with a different configuration. C-DEngine runs once per process, " +
                    "so the configuration it was started with stays in effect.");
            }

            _leaseCount++;
            logger.LogDebug("Acquired a lease on the C-DEngine node, {LeaseCount} active.", _leaseCount);

            return new CdeNodeLease(Release);
        }
    }

    private void Release()
    {
        IDisposable? runtime;

        lock (_sync)
        {
            _leaseCount--;
            if (_leaseCount > 0)
            {
                return;
            }

            runtime = _runtime;
            _runtime = null;
            _shutDown = true;
        }

        // Outside the lock: the shutdown waits for PreShutdownDelay, and an Acquire racing it fails anyway.
        runtime?.Dispose();
    }
}