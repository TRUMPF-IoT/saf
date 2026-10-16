// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer.Messages;

/// <summary>
/// The text payload of a file chunk request whose data travels as binary payload.
/// </summary>
internal class SendBinaryFileChunkRequest
{
    public required TransportFile File { get; set; }
    public required uint Index { get; set; }
}
