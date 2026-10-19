// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public enum FileReceiverStatus
{
    Ok,
    /// <summary>The chunk was not written; sending it again can succeed.</summary>
    Failed,
    /// <summary>
    /// All chunks arrived, but the file does not match the sender's length or content hash. Sending a chunk again
    /// cannot succeed; the next transfer of the file starts over.
    /// </summary>
    ContentMismatch
}