// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public interface IFileSender : IDisposable
{
    Task<FileTransferStatus> SendAsync(string topic, string fullFilePath, uint timeoutMs);
    Task<FileTransferStatus> SendAsync(string topic, string fullFilePath, uint timeoutMs, IDictionary<string, string> properties);
}