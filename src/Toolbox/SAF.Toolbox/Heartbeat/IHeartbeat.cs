// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;

namespace SAF.Toolbox.Heartbeat
{
    /// <summary>
    /// Heartbeat service to provide a more testable "timer" for consuming services.
    /// </summary>
    /// <remarks>
    /// Be careful when using one heartbeat instance for several cyclic tasks as all added Beat
    /// event handler will be called sequentially. For example a long running Beat event handler might block
    /// other short running ones.</remarks>
    public interface IHeartbeat
    {
        /// <summary>
        /// Fired on heart beat.
        /// </summary>
        event EventHandler<HeartbeatEventArgs> Beat;

        /// <summary>
        /// Gets the beat cycle time in milliseconds.
        /// </summary>
        int BeatCycleTimeMillis { get; }

        /// <summary>
        /// Gets the current beat counter.
        /// </summary>
        long CurrentBeat { get; }

        /// <summary>
        /// Provides a conversion method from configured beat cycles to wall clock time. 
        /// Use this to check against time units.
        /// </summary>
        /// <param name="beatCount">The beat count to convert.</param>
        /// <returns>Time span since the heartbeat service has started.</returns>
        TimeSpan WallClockTimeAgo(long beatCount);
    }
}
