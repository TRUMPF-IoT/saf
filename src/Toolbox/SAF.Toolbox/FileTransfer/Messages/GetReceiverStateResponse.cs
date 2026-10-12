// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Messaging.Contracts;

namespace SAF.Toolbox.FileTransfer.Messages;

public class GetReceiverStateResponse
{
    public required FileReceiverState State { get; set; }

    /// <summary>
    /// Gets or sets the payload formats the receiver reads file chunks in.
    /// <c>null</c> from receivers on SAF 10.x or older, which read text only.
    /// </summary>
    public MessageFormats? AcceptedChunkFormats { get; set; }
}
