// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SAF.Toolbox.Heartbeat;

namespace SAF.Toolbox.FileTransfer;

public class StatefulFileReceiverFactory(
    ILoggerFactory loggerFactory,
    IFileSystem fileSystem,
    IHeartbeatPool heartbeatPool,
    IOptions<FileReceiverOptions> options) : IStatefulFileReceiverFactory
{
    public IStatefulFileReceiver CreateForFolder(string folderPath)
    {
        return new StatefulFileReceiver(loggerFactory.CreateLogger<StatefulFileReceiver>(), fileSystem, options.Value, heartbeatPool, folderPath);
    }
}