// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using System.Threading;

namespace SAF.Toolbox.Heartbeat
{
    internal class Heartbeat : IHeartbeat, IDisposable
    {
        public event EventHandler<HeartbeatEventArgs> Beat;
        public int BeatCycleTimeMillis { get; }
        public long CurrentBeat { get; private set; }

        private readonly Timer _heartbeatTimer;

        public Heartbeat(int beatCycleTimeMillis)
        {
            BeatCycleTimeMillis = beatCycleTimeMillis;
            _heartbeatTimer = new Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(beatCycleTimeMillis));
        }

        public TimeSpan WallClockTimeAgo(long beatCount)
            => TimeSpan.FromMilliseconds(beatCount * BeatCycleTimeMillis);

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
        }

        private void Tick(object state)
            => Beat?.Invoke(this, new HeartbeatEventArgs(BeatCycleTimeMillis, ++CurrentBeat));
    }
}
