// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public class BeforeFileReceivedEventArgs(TransportFile file) : EventArgs
{
    public TransportFile File { get; } = file;
    public bool AllowOverwrite { get; set; } = true;
}

public class FileReceivedEventArgs(TransportFile file, string localFileFullName) : EventArgs
{
    public TransportFile File { get; } = file;
    public string LocalFileFullName { get; } = localFileFullName;
}