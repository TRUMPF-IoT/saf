// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Toolbox.FileTransfer;

public interface IFileSender : IDisposable
{
    Task<FileTransferStatus> Send(string topic, string fileName, string mimeType, Stream stream);
    Task<FileTransferStatus> Send(string topic, string fileName, string mimeType, Stream stream, ulong timeoutMs);
    Task<FileTransferStatus> Send(string topic, TransportFile file);
    Task<FileTransferStatus> Send(string topic, TransportFile file, ulong timeoutMs);
    Task<FileTransferStatus> SendInChunks(string topic, string filePath, IDictionary<string, string>? properties = null);
    Task<FileTransferStatus> SendInChunks(string topic, string filePath, ulong timeoutMs, IDictionary<string, string>? properties = null);
}