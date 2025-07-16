// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public enum FileTransferStatus
{
    Delivered,
    TimedOut,
    Error,
    FileNotFound
}