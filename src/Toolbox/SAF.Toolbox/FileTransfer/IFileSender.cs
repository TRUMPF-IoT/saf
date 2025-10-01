// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public interface IFileSender : IDisposable
{
    Task<FileTransferStatus> SendAsync(string topic, string fullFilePath, uint timeoutMs);
    Task<FileTransferStatus> SendAsync(string topic, string fullFilePath, uint timeoutMs, IDictionary<string, string> properties);
}