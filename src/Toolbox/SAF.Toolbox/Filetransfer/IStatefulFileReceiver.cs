// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;

namespace SAF.Toolbox.Filetransfer
{
    public interface IStatefulFileReceiver
    {
        event Action<string> FileReceived;
        event Action<string> StreamReceived;

        void WriteFile(string folderPath, TransportFileDelivery delivery, bool overwrite);

        [Obsolete("WriteFile(string, TransportFileDelivery) is deprecated, please use WriteFile(string, TransportFileDelivery, bool) instead.")]
        void WriteFile(string folderPath, TransportFileDelivery delivery);
    }
}