// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Messaging.Contracts;

namespace SAF.Toolbox.FileTransfer.Messages;

internal class SendFileChunkRequest : MessageRequestBase
{
    public required TransportFile File { get; set; }
    public FileChunk? FileChunk { get; set; }
}