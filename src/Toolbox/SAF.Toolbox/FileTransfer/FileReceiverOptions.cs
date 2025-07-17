// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public class FileReceiverOptions
{
    /// <summary>
    /// Configures the maximum age of the state of received files (temp and metadata files) before they are considered expired and cleaned up.
    /// Default: 3 days. Set to 0 to disable expiration.
    /// </summary>
    public uint StateExpirationAfterHours { get; set; } = 72;
}