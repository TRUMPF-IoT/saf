// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer
{
    public interface IStatefulFileReceiver
    {
        event Action<string>? FileReceived;
        event Action<string>? StreamReceived;

        void WriteFile(string folderPath, TransportFileDelivery delivery, bool overwrite);
    }
}