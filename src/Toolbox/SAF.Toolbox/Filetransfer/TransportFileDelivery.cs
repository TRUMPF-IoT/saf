// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System;

namespace SAF.Toolbox.FileTransfer
{
    public class TransportFileDelivery
    {
        public bool IsConsistent;
        public DateTimeOffset Timestamp;
        public string Channel;
        public TransportFile TransportFile;
    }
}