// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using nsCDEngine.BaseClasses;
using System.Collections.Generic;

namespace SAF.Communication.PubSub.Cde
{
    internal interface IRemoteSubscriber
    {
        TSM Tsm { get; }
        bool IsLocalHost { get; }
        bool IsAlive { get; }
        bool IsRegistry { get; }
        string TargetEngine { get; }
        string Version { get; }

        void AddPatterns(IList<string> patterns);
        void RemovePatterns(IList<string> patterns);
        bool HasPatterns { get; }

        bool IsMatch(string topic);

        void Touch();
    }
}