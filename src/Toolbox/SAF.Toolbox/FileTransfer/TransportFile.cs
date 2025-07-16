// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public class TransportFile
{
    public required string FileName { get; set; }
    public required string FileId { get; set; }
    public required string ContentHash { get; set; }
    public required long ContentLength { get; set; }
    public required uint ChunkSize { get; set; }
    public required uint TotalChunks { get; set; }

    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}