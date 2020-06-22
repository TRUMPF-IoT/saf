// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common
{
    /// <summary>
    ///     Represents a hosted "microservice" within the SAF infrastructure.
    /// </summary>
    public interface IHostedService
    {
        /// <summary>
        ///     Starts the service.
        /// </summary>
        void Start();

        /// <summary>
        ///     Stops the service.
        /// </summary>
        void Stop();

        /// <summary>
        ///     Kills the service.
        /// </summary>
        void Kill();
    }
}