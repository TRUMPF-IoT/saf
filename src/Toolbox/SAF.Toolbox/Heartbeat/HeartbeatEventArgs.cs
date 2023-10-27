// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Heartbeat
{
    public class HeartbeatEventArgs : EventArgs
    {
        public int BeatCycleTimeMillis { get; }
        public long CurrentBeat { get; }

        public HeartbeatEventArgs(int beatCycleTimeMillis, long currentBeat)
        {
            BeatCycleTimeMillis = beatCycleTimeMillis;
            CurrentBeat = currentBeat;
        }
    }
}
