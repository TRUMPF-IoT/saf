// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;

namespace SAF.Toolbox.Heartbeat
{
    /// <summary>
    /// A heartbeat pool that can be used to create one single heartbeat instance for each cycle used in a micro service.
    /// This should be used with care as each new cycle results in the creation of a new System.Threading.Timer instance.
    /// </summary>
    /// <remarks>
    /// In case the heartbeat pool gets disposed, all heartbeats owned from that pool will be disposed too.
    /// </remarks>
    public interface IHeartbeatPool : IDisposable
    {
        /// <summary>
        /// Gets an existing heartbeat for the specified cycle, or in case the heartbeat for that cycle doesn't exists
        /// creates and returns a new one.
        /// </summary>
        /// <param name="heartbeatMillis">The cycle the heartbeat should run with.</param>
        /// <returns>A heartbeat instance.</returns>
        IHeartbeat GetOrCreateHeartbeat(int heartbeatMillis);
    }
}