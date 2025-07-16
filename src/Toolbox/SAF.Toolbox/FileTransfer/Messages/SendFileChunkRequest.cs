// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common.Contracts;

namespace SAF.Toolbox.FileTransfer.Messages;

internal class SendFileChunkRequest : MessageRequestBase
{
    public required TransportFile File { get; set; }
    public FileChunk? FileChunk { get; set; }
}