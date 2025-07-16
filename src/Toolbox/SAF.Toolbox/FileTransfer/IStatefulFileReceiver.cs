// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public interface IStatefulFileReceiver
{
    event Action<string>? FileReceived;

    FileReceiverState GetState(string folderPath, TransportFile file);
    FileReceiverStatus WriteFile(string folderPath, TransportFile file, FileChunk fileChunk);
}