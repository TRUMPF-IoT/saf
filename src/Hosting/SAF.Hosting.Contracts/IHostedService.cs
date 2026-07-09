// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting.Contracts;

/// <summary>
/// Represents a hosted plug-in service within the SAF infrastructure.
/// </summary>
public interface IHostedService
{
    /// <summary>
    /// Starts the service.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the service.
    /// </summary>
    void Stop();

    /// <summary>
    /// Kills the service.
    /// </summary>
    void Kill();
}