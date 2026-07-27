// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts;

/// <summary>
/// Default delegate-based implementation of <see cref="IMessagingInfrastructureFactory"/>.
/// </summary>
public sealed class DelegatingMessagingInfrastructureFactory(string key, Func<MessagingConfiguration, IMessagingInfrastructure> factory) : IMessagingInfrastructureFactory
{
    private readonly Func<MessagingConfiguration, IMessagingInfrastructure> _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    /// <inheritdoc />
    public string Key { get; } = key ?? throw new ArgumentNullException(nameof(key));

    /// <inheritdoc />
    public IMessagingInfrastructure Create(MessagingConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return _factory(configuration);
    }
}
