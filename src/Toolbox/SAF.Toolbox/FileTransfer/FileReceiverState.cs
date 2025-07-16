// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public class FileReceiverState
{
    public bool FileExists { get; set; }
    public HashSet<uint> TransmittedChunks { get; set; } = [];
}