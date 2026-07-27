// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts;

/// <summary>
/// Creates configured messaging infrastructure instances for a specific backend key.
/// </summary>
public interface IMessagingInfrastructureFactory
{
    /// <summary>
    /// Gets the backend key exposed by this factory.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Creates a messaging infrastructure instance for the specified backend configuration.
    /// </summary>
    /// <param name="configuration">The backend configuration.</param>
    /// <returns>The created messaging infrastructure instance.</returns>
    IMessagingInfrastructure Create(MessagingConfiguration configuration);
}
