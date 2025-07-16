// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer.Messages;

public class GetReceiverStateResponse
{
    public required FileReceiverState State { get; set; }
}