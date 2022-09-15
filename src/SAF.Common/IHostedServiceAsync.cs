// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common;

/// <summary>
/// Represents a hosted "microservice" within the SAF infrastructure that can be started and stopped asynchronously.
/// </summary>
public interface IHostedServiceAsync : IHostedServiceBase
{
    /// <summary>
    /// Starts the service.
    /// </summary>
    Task StartAsync(CancellationToken cancelToken);

    /// <summary>
    /// Stops the service.
    /// </summary>
    Task StopAsync(CancellationToken cancelToken);
}