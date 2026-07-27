// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer.Messages;

internal class SendFileChunkResponse
{
    public FileReceiverStatus Status { get; set; }
}