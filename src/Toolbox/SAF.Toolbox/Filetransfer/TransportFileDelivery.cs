// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Toolbox.FileTransfer;

public class TransportFileDelivery
{
    public bool IsConsistent { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Channel { get; set; } = default!;
    public TransportFile TransportFile { get; set; } = default!;
}