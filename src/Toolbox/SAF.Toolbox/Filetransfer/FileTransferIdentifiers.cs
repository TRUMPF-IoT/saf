// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public static class FileTransferIdentifiers
{
    public const string FullPath = "full path";
    public const string Chunked = "chunked";
    public const string FileStream = "filestream";
    public const string LastChunk = "last chunk";
    public const string ChunkNumber = "chunk number";
    public const string FileSize = "file size";
    public const string ChunkSize = "chunk size";
    public const string ChunkOffset = "chunk offset";
    public const string TransferId = "transfer id";
}

public class FileTransferProperties
{
    public string FullPath { get; set; } = default!;
    public bool IsChunked { get; set; }
    public bool IsFileStream { get; set; }
    public bool LastChunk { get; set; }
    public long ChunkNumber { get; set; }
    public long FileSize { get; set; }
    public long ChunkSize { get; set; }
    public long ChunkOffset { get; set; }
    public long? TransferId { get; set; }
}

public static class FileTransferExtensions
{
    public static FileTransferProperties FromDictionary(this IDictionary<string, string> props)
    {
        var fileTransferProperties = new FileTransferProperties
        {
            IsChunked = props.ContainsKey(FileTransferIdentifiers.Chunked),
            IsFileStream = props.ContainsKey(FileTransferIdentifiers.FileStream),
            FullPath = props.TryGetValue(FileTransferIdentifiers.FullPath, out var fullPathValue) ? fullPathValue : string.Empty,
            LastChunk = props.TryGetValue(FileTransferIdentifiers.LastChunk, out var lastChunkValue) && Convert.ToBoolean(lastChunkValue),
            FileSize = props.TryGetValue(FileTransferIdentifiers.FileSize, out var fileSizeValue) ? long.TryParse(fileSizeValue, out var fsval) ? fsval : 0 : 0,
            ChunkSize = props.TryGetValue(FileTransferIdentifiers.ChunkSize, out var chunkSizeValue) ? long.TryParse(chunkSizeValue, out var csval) ? csval : 0 : 0,
            ChunkOffset = props.TryGetValue(FileTransferIdentifiers.ChunkOffset, out var chunkOffsetValue) ? long.TryParse(chunkOffsetValue, out var coval) ? coval : 0 : 0,
            ChunkNumber = props.TryGetValue(FileTransferIdentifiers.ChunkNumber, out var chunkNumberValue) ? long.TryParse(chunkNumberValue, out var icval) ? icval : 0 : 0,
            TransferId = props.TryGetValue(FileTransferIdentifiers.TransferId, out var transferId) ? Convert.ToInt64(transferId) : (long?)null
        };
        return fileTransferProperties;
    }
}