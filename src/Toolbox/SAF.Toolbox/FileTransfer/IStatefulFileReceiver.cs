// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public interface IStatefulFileReceiver : IDisposable
{
    FileReceiverState GetState(TransportFile file);
    FileReceiverStatus WriteFile(TransportFile file, FileChunk fileChunk);

    event EventHandler<BeforeFileReceivedEventArgs>? BeforeFileReceived;
    event EventHandler<FileReceivedEventArgs>? FileReceived;
}