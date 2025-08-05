// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public class FileSenderOptions
{
    public uint MaxChunkSizeInBytes { get; set; } = 200 * 1024; // 200 kB
    public int RetryAttemptsForFailedChunks { get; set; } = 0;
}