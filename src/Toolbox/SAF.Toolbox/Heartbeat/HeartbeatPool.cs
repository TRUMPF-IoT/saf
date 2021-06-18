// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Concurrent;
using System.Linq;

namespace SAF.Toolbox.Heartbeat
{
    internal class HeartbeatPool : IHeartbeatPool
    {
        private readonly ConcurrentDictionary<int, Heartbeat> _heartbeatsPerCycle = new();

        public IHeartbeat GetOrCreateHeartbeat(int heartbeatMillis)
            => _heartbeatsPerCycle.GetOrAdd(heartbeatMillis, cycle => new Heartbeat(cycle));

        public void Dispose()
        {
            var heartbeats = _heartbeatsPerCycle.Values.ToArray();
            _heartbeatsPerCycle.Clear();

            foreach (var heartbeat in heartbeats)
            {
                heartbeat.Dispose();
            }
        }
    }
}